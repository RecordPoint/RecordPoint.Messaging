using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MysticMind.PostgresEmbed;
using Npgsql;
using RecordPoint.Messaging.AzureServiceBus.Test;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.IntegrationTest
{

    public class EmbeddedDatabaseFixture : IDisposable
    {
        public PgServer PgServer { get; set; }
        public bool IsInstalled { get; set; } = false;

        private const string PostgreSQLVersion = "10.6.1.0";
        private const int Port = 6000;

        public EmbeddedDatabaseFixture()
        {
            PgServer = new PgServer(PostgreSQLVersion,
                port: Port,
                clearInstanceDirOnStop: true,
                addLocalUserAccessPermission: true);
            PgServer.Start();
        }

        public void Dispose()
        {
            if (PgServer != null)
            {
                PgServer.Stop();
            }
        }
    }

    [Transactional]
    public class TransactionalHandlerWithDbTransaction : IMessageHandler
    {
        public ConcurrentBag<DateTime> Calls { get; set; } = new ConcurrentBag<DateTime>();
        public ConcurrentBag<DateTime> CommittedTransactions { get; set; } = new ConcurrentBag<DateTime>();

        public async Task HandleMessage(object message, IMessageProcessingContext context)
        {
            var testMessage = message as TestMessage1;

            Calls.Add(DateTime.UtcNow);
            
            using (var connection = new NpgsqlConnection("Server=localhost;Port=6000;User Id=postgres;Password=test;Database=postgres"))
            {
                connection.Open();

                using (var tx = connection.BeginTransaction())
                {
                    tx?.Commit();
                }
            }
            
            await context.Complete();

            CommittedTransactions.Add(DateTime.UtcNow);
        }
    }

    [TestClass]
    public class TransactionalHandlerWithDatabaseTransactionTest
    {
        private static EmbeddedDatabaseFixture _embeddedDatabase;

        [ClassInitialize]
        public static void Classinitialize(TestContext tc)
        {
            _embeddedDatabase = new EmbeddedDatabaseFixture();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _embeddedDatabase.Dispose();
            _embeddedDatabase = null;
        }

        [TestMethod]
        [Ignore]    // hangs in the CI build
        public async Task TransactionalHandlerFailsWithExplicitDbTransactions()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new TransactionalHandlerWithDbTransaction());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            var sender = factory.CreateMessageSender("my-first-queue");

            var transactionalHandler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage1)) as TransactionalHandlerWithDbTransaction;

            await sender.Send(new TestMessage1());

            await TestHelpers.PumpQueueUntil(factory, "my-first-queue", () =>
            {
                return transactionalHandler.Calls.Count == 2;
            });

            Assert.AreEqual(0, transactionalHandler.CommittedTransactions.Count);
        }
    }
}

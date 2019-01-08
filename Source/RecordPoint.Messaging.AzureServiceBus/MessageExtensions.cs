using Microsoft.Azure.ServiceBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public static class MessageExtensions
    {
        public static string GetTenantId(this Message message)
        {
            if (message.UserProperties.TryGetValue(Constants.HeaderKeys.RPTenantId, out var tenatId))
            {
                return tenatId.ToString();
            }
            return null;
        }
    }
}

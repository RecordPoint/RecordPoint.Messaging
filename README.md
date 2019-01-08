# RecordPoint.Messaging
This library provides an abstraction layer over messaging middleware in C#, along with an implementation using Azure Service Bus. 

This library uses [Semantic Versioning](https://semver.org/).

# Running the Tests

The integration tests use real Azure Service Bus and real Azure Blob Storage services to test against. Connection strings for these two services need to be set in environment variables in order for the tests to run. Set the following environment variables before running the tests: 
   * RP_M_SBCONNECTION=[service bus connection string]
   * RP_M_BLOBCONNECTION=[blob storage connection string]
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure services
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var db = postgres.AddDatabase("b2bsample");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var redis = builder.AddRedis("redis");

// B2B Sample Host — references the infrastructure services
builder.AddProject<Projects.B2BSample_Host>("b2bsample")
    .WithReference(db)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(rabbitmq);

await builder.Build().RunAsync().ConfigureAwait(false);

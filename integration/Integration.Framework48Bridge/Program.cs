using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using RabbitMQ.Client;
using Integration.CloudEventsAdapter;
using Chassis.SharedKernel.Contracts;

namespace Integration.Framework48Bridge
{
    /// <summary>
    /// .NET Framework 4.8 bridge service.
    /// Watches stdin (or a watched folder) for legacy SOAP/XML envelopes,
    /// maps them to <see cref="CloudEventEnvelope"/>, and publishes to RabbitMQ
    /// on exchange <c>legacy-bridge</c> with routing key <c>legacy.{type}</c>.
    /// </summary>
    internal sealed class Program
    {
        private const string ExchangeName = "legacy-bridge";
        private const string RoutingKeyPrefix = "legacy.";

        private static readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private static void Main(string[] args)
        {
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true; // Prevent immediate exit; let graceful shutdown run.
                Console.WriteLine("[bridge] Shutdown requested (Ctrl+C).");
                _cts.Cancel();
            };

            Console.WriteLine("[bridge] .NET Framework 4.8 legacy integration bridge starting.");

            var rabbitHost = GetConfig("RABBITMQ_HOST", "localhost");
            var rabbitUser = GetConfig("RABBITMQ_USER", "guest");
            var rabbitPass = GetConfig("RABBITMQ_PASS", "guest");

            var factory = new ConnectionFactory
            {
                HostName = rabbitHost,
                UserName = rabbitUser,
                Password = rabbitPass,
                AutomaticRecoveryEnabled = true,
            };

            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.ExchangeDeclare(
                    exchange: ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                Console.WriteLine($"[bridge] Connected to RabbitMQ at {rabbitHost}. Reading from stdin...");
                Console.WriteLine("[bridge] Paste XML legacy events (one per line), press Ctrl+C to exit.");

                while (!_cts.IsCancellationRequested)
                {
                    string? line;
                    try
                    {
                        line = Console.ReadLine();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[bridge] Read error: {ex.Message}");
                        break;
                    }

                    if (line == null)
                    {
                        // stdin closed (piped input exhausted).
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var envelope = ParseLegacySoapEnvelope(line);
                        var cloudEvent = CloudEventSerializer.FromEnvelope(envelope);
                        var headers = CloudEventsMassTransitBridge.ToPublishHeaders(cloudEvent);
                        var body = SerializeToJson(envelope.Data);

                        var props = channel.CreateBasicProperties();
                        props.ContentType = "application/json";
                        props.DeliveryMode = 2; // Persistent.
                        props.Headers = new System.Collections.Generic.Dictionary<string, object>();
                        foreach (var header in headers)
                        {
                            if (!string.IsNullOrEmpty(header.Value))
                            {
                                props.Headers[header.Key] = header.Value;
                            }
                        }

                        var routingKey = RoutingKeyPrefix + SanitizeRoutingKey(envelope.Type);
                        channel.BasicPublish(
                            exchange: ExchangeName,
                            routingKey: routingKey,
                            mandatory: false,
                            basicProperties: props,
                            body: Encoding.UTF8.GetBytes(body));

                        Console.WriteLine($"[bridge] Published: routingKey={routingKey} id={envelope.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[bridge] Failed to process line: {ex.Message}");
                    }
                }
            }

            Console.WriteLine("[bridge] Shutdown complete.");
        }

        /// <summary>
        /// Parses a minimal legacy SOAP/XML envelope and maps it to a <see cref="CloudEventEnvelope"/>.
        /// Expected XML structure (simplified SOAP-style):
        /// <code>
        /// &lt;LegacyEvent type="order.created" source="/legacy/orders"&gt;
        ///   &lt;Payload&gt;...&lt;/Payload&gt;
        /// &lt;/LegacyEvent&gt;
        /// </code>
        /// </summary>
        private static CloudEventEnvelope ParseLegacySoapEnvelope(string xml)
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root ?? throw new InvalidOperationException("XML has no root element.");

            var type = (string?)root.Attribute("type")
                ?? throw new InvalidOperationException("Missing 'type' attribute on root element.");

            var source = (string?)root.Attribute("source") ?? "/legacy/bridge";
            var id = (string?)root.Attribute("id") ?? Guid.NewGuid().ToString("D");

            var payloadElement = root.Element("Payload");
            object? payload = payloadElement != null
                ? (object)payloadElement.ToString()
                : null;

            return new CloudEventEnvelope(
                id: id,
                source: source,
                type: type,
                data: payload,
                time: DateTimeOffset.UtcNow,
                dataContentType: "application/xml");
        }

        private static string SerializeToJson(object? data)
        {
            return data != null
                ? JsonConvert.SerializeObject(data)
                : "null";
        }

        private static string SanitizeRoutingKey(string type)
        {
            // Replace characters invalid in AMQP routing keys with hyphens.
            return type.Replace(' ', '-').Replace('/', '-').ToLowerInvariant();
        }

        private static string GetConfig(string envVarName, string defaultValue)
        {
            var value = System.Configuration.ConfigurationManager.AppSettings[envVarName]
                ?? Environment.GetEnvironmentVariable(envVarName);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }
    }
}

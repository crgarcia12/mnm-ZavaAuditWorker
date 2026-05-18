using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using RabbitMQ.Client;

namespace ZavaBank.ZavaAuditWorker
{
    internal sealed class AuditEvent
    {
        public string EventType;
        public string ServiceName;
        public string UserId;
        public DateTime Timestamp;
        public string DetailsJson;
        public string CorrelationId;
    }

    internal class Program
    {
        private static volatile bool _keepRunning = true;
        private static string _rabbitHost;
        private static int _rabbitPort;
        private static string _rabbitVHost;
        private static string _rabbitUser;
        private static string _rabbitPassword;
        private static string _auditQueue;
        private static string _deadLetterExchange;
        private static int _pollIntervalMs;
        private static string _connectionString;

        private static void Main(string[] args)
        {
            LoadConfig();

            Console.CancelKeyPress += OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += delegate { _keepRunning = false; };

            Console.WriteLine("[AuditWorker] Starting audit worker.");
            Console.WriteLine("[AuditWorker] Queue={0}, RabbitMQ={1}:{2}", _auditQueue, _rabbitHost, _rabbitPort);

            while (_keepRunning)
            {
                try
                {
                    PollQueue();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[AuditWorker] Poll cycle failed: " + ex.Message);
                }

                if (_keepRunning)
                {
                    Thread.Sleep(_pollIntervalMs);
                }
            }

            Console.WriteLine("[AuditWorker] Graceful shutdown complete.");
        }

        private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("[AuditWorker] Ctrl+C received. Stopping...");
            _keepRunning = false;
            e.Cancel = true;
        }

        private static void LoadConfig()
        {
            _rabbitHost = Env("RABBITMQ_HOST", "RabbitMQ.Host", "rabbitmq");
            _rabbitPort = EnvInt("RABBITMQ_PORT", "RabbitMQ.Port", 5672);
            _rabbitVHost = Env("RABBITMQ_VHOST", "RabbitMQ.VHost", "/zavabank");
            _rabbitUser = Env("RABBITMQ_USER", "RabbitMQ.User", "zava_app");
            _rabbitPassword = Env("RABBITMQ_PASSWORD", "RabbitMQ.Password", "zava_pass");
            _auditQueue = Env("AUDIT_QUEUE", "RabbitMQ.AuditQueue", "audit.events");
            _deadLetterExchange = Env("RABBITMQ_DLX", "RabbitMQ.DeadLetterExchange", "zava.dlx");
            _pollIntervalMs = EnvInt("WORKER_POLL_INTERVAL_MS", "Worker.PollIntervalMs", 5000);
            _connectionString = BuildSqlConnectionString();
        }

        private static string BuildSqlConnectionString()
        {
            var configured = Env("DB_CONNECTION_STRING", "Database.ConnectionString", string.Empty);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            var host = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_HOST"), Environment.GetEnvironmentVariable("DATABASE_HOST"), "sqlserver");
            var port = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_PORT"), Environment.GetEnvironmentVariable("DATABASE_PORT"), "1433");
            var database = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_NAME"), Environment.GetEnvironmentVariable("DATABASE_NAME"), "ZavaBankDB");
            var user = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_USER"), Environment.GetEnvironmentVariable("DATABASE_USER"), "sa");
            var password = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_PASSWORD"), Environment.GetEnvironmentVariable("DATABASE_PASSWORD"), "Zava123!");
            return string.Format("Server={0},{1};Database={2};User ID={3};Password={4};Encrypt=False;TrustServerCertificate=True;", host, port, database, user, password);
        }

        private static string Env(string envKey, string appSettingKey, string defaultValue)
        {
            var env = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
            }

            var appSetting = ConfigurationManager.AppSettings[appSettingKey];
            if (!string.IsNullOrWhiteSpace(appSetting))
            {
                return appSetting;
            }

            return defaultValue;
        }

        private static int EnvInt(string envKey, string appSettingKey, int defaultValue)
        {
            var value = Env(envKey, appSettingKey, defaultValue.ToString());
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : defaultValue;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static ConnectionFactory CreateFactory()
        {
            return new ConnectionFactory
            {
                HostName = _rabbitHost,
                Port = _rabbitPort,
                VirtualHost = _rabbitVHost,
                UserName = _rabbitUser,
                Password = _rabbitPassword,
                AutomaticRecoveryEnabled = false
            };
        }

        private static void PollQueue()
        {
            using (var connection = CreateFactory().CreateConnection())
            using (var channel = connection.CreateModel())
            {
                EnsureQueue(channel, _auditQueue, _deadLetterExchange);

                var result = channel.BasicGet(_auditQueue, false);
                if (result == null)
                {
                    Console.WriteLine("[AuditWorker] No audit events available.");
                    return;
                }

                var body = Encoding.UTF8.GetString(result.Body);
                Console.WriteLine("[AuditWorker] Received event deliveryTag={0}", result.DeliveryTag);

                try
                {
                    var auditEvent = ParseAuditEvent(body);
                    SaveAuditEvent(auditEvent);
                    channel.BasicAck(result.DeliveryTag, false);
                    Console.WriteLine(
                        "[AuditWorker] Saved audit event type={0}, service={1}, user={2}, correlation={3}",
                        auditEvent.EventType,
                        auditEvent.ServiceName,
                        auditEvent.UserId,
                        auditEvent.CorrelationId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[AuditWorker] Failed to process event: " + ex.Message);
                    channel.BasicNack(result.DeliveryTag, false, false);
                }
            }
        }

        private static void EnsureQueue(IModel channel, string queueName, string deadLetterExchange)
        {
            try
            {
                channel.QueueDeclarePassive(queueName);
            }
            catch
            {
                var args = new Dictionary<string, object> { { "x-dead-letter-exchange", deadLetterExchange } };
                channel.QueueDeclare(queueName, true, false, false, args);
                Console.WriteLine("[AuditWorker] Declared queue {0}", queueName);
            }
        }

        private static AuditEvent ParseAuditEvent(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException("Audit message body is empty.");
            }

            if (raw.TrimStart().StartsWith("<"))
            {
                return ParseXml(raw);
            }

            return ParseJson(raw);
        }

        private static AuditEvent ParseJson(string raw)
        {
            var serializer = new JavaScriptSerializer();
            var data = serializer.Deserialize<Dictionary<string, object>>(raw);
            var parsedTime = ParseTimestamp(ReadValue(data, "timestamp", "Timestamp"));

            return new AuditEvent
            {
                EventType = ReadValue(data, "eventType", "EventType", "action"),
                ServiceName = ReadValue(data, "serviceName", "ServiceName", "service"),
                UserId = ReadValue(data, "userId", "UserId", "userName"),
                Timestamp = parsedTime,
                DetailsJson = raw,
                CorrelationId = ReadValue(data, "correlationId", "CorrelationId", "requestId")
            };
        }

        private static AuditEvent ParseXml(string raw)
        {
            var xml = XDocument.Parse(raw);
            Func<string, string> value = name =>
            {
                var element = xml.Root != null ? xml.Root.Element(name) : null;
                return element != null ? element.Value : string.Empty;
            };

            return new AuditEvent
            {
                EventType = FirstNonEmpty(value("EventType"), value("Action"), value("Type")),
                ServiceName = FirstNonEmpty(value("ServiceName"), value("Service")),
                UserId = FirstNonEmpty(value("UserId"), value("UserName")),
                Timestamp = ParseTimestamp(value("Timestamp")),
                DetailsJson = raw,
                CorrelationId = FirstNonEmpty(value("CorrelationId"), value("RequestId"))
            };
        }

        private static DateTime ParseTimestamp(string value)
        {
            DateTime timestamp;
            return DateTime.TryParse(value, out timestamp) ? timestamp : DateTime.Now;
        }

        private static string ReadValue(IDictionary<string, object> data, params string[] keys)
        {
            foreach (var key in keys)
            {
                object value;
                if (data.TryGetValue(key, out value) && value != null)
                {
                    return value.ToString();
                }
            }

            return string.Empty;
        }

        private static void SaveAuditEvent(AuditEvent auditEvent)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                if (HasLegacyAuditColumns(connection))
                {
                    InsertLegacyAuditRow(connection, auditEvent);
                }
                else
                {
                    InsertModernAuditRow(connection, auditEvent);
                }
            }
        }

        private static bool HasLegacyAuditColumns(SqlConnection connection)
        {
            using (var command = new SqlCommand("SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AuditLog' AND COLUMN_NAME = 'ActionID';", connection))
            {
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static int EnsureAction(SqlConnection connection, string eventType)
        {
            var action = string.IsNullOrWhiteSpace(eventType) ? "UNKNOWN" : eventType;

            using (var findCommand = new SqlCommand("SELECT ActionID FROM AuditActions WHERE ActionName = @ActionName;", connection))
            {
                findCommand.Parameters.AddWithValue("@ActionName", action);
                var existing = findCommand.ExecuteScalar();
                if (existing != null && existing != DBNull.Value)
                {
                    return Convert.ToInt32(existing);
                }
            }

            using (var insertCommand = new SqlCommand("INSERT INTO AuditActions (ActionName, Description, Severity) VALUES (@ActionName, @Description, @Severity); SELECT CAST(SCOPE_IDENTITY() AS INT);", connection))
            {
                insertCommand.Parameters.AddWithValue("@ActionName", action);
                insertCommand.Parameters.AddWithValue("@Description", "Auto-created by ZavaAuditWorker");
                insertCommand.Parameters.AddWithValue("@Severity", "Info");
                return Convert.ToInt32(insertCommand.ExecuteScalar());
            }
        }

        private static void InsertLegacyAuditRow(SqlConnection connection, AuditEvent auditEvent)
        {
            var actionId = EnsureAction(connection, auditEvent.EventType);

            using (var command = new SqlCommand(
                "INSERT INTO AuditLog (ActionID, UserName, IPAddress, TableName, RecordID, OldValues, NewValues, Description, [Timestamp], SessionID, MachineName) " +
                "VALUES (@ActionID, @UserName, @IPAddress, @TableName, @RecordID, @OldValues, @NewValues, @Description, @Timestamp, @SessionID, @MachineName);",
                connection))
            {
                command.Parameters.AddWithValue("@ActionID", actionId);
                command.Parameters.AddWithValue("@UserName", (object)ValueOrNull(auditEvent.UserId) ?? DBNull.Value);
                command.Parameters.AddWithValue("@IPAddress", DBNull.Value);
                command.Parameters.AddWithValue("@TableName", (object)ValueOrNull(auditEvent.ServiceName) ?? DBNull.Value);
                command.Parameters.AddWithValue("@RecordID", (object)ValueOrNull(auditEvent.CorrelationId) ?? DBNull.Value);
                command.Parameters.AddWithValue("@OldValues", DBNull.Value);
                command.Parameters.AddWithValue("@NewValues", (object)ValueOrNull(auditEvent.DetailsJson) ?? DBNull.Value);
                command.Parameters.AddWithValue("@Description", (object)ValueOrNull(auditEvent.EventType) ?? DBNull.Value);
                command.Parameters.AddWithValue("@Timestamp", auditEvent.Timestamp);
                command.Parameters.AddWithValue("@SessionID", (object)ValueOrNull(auditEvent.CorrelationId) ?? DBNull.Value);
                command.Parameters.AddWithValue("@MachineName", Environment.MachineName);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertModernAuditRow(SqlConnection connection, AuditEvent auditEvent)
        {
            using (var command = new SqlCommand(
                "INSERT INTO AuditLog (EventType, ServiceName, UserId, [Timestamp], Details, CorrelationId) " +
                "VALUES (@EventType, @ServiceName, @UserId, @Timestamp, @Details, @CorrelationId);",
                connection))
            {
                command.Parameters.AddWithValue("@EventType", (object)ValueOrNull(auditEvent.EventType) ?? DBNull.Value);
                command.Parameters.AddWithValue("@ServiceName", (object)ValueOrNull(auditEvent.ServiceName) ?? DBNull.Value);
                command.Parameters.AddWithValue("@UserId", (object)ValueOrNull(auditEvent.UserId) ?? DBNull.Value);
                command.Parameters.AddWithValue("@Timestamp", auditEvent.Timestamp);
                command.Parameters.AddWithValue("@Details", (object)ValueOrNull(auditEvent.DetailsJson) ?? DBNull.Value);
                command.Parameters.AddWithValue("@CorrelationId", (object)ValueOrNull(auditEvent.CorrelationId) ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        private static object ValueOrNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}

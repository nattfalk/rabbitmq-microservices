﻿using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;

class Receive
{
    private static TaskCompletionSource _tcs = new TaskCompletionSource();
    private static bool _sigintReceived = false;

    private static void Initialize()
    {
        Console.WriteLine("Waiting for SIGINT/SIGTERM");

        Console.CancelKeyPress += (_, ea) => 
        {
            ea.Cancel = true;

            Console.WriteLine("Received SIGINT (Ctrl+C)");

            _tcs.SetResult();
            _sigintReceived = true;
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => {
            if (!_sigintReceived)
            {
                Console.WriteLine("Received SIGTERM");
                _tcs.SetResult();
            }
            else
            {
                Console.WriteLine("Received SIGTERM, ignoring it because already prcessed SIGINT");
            }
        };
    }

    private static void Cleanup()
    {
    }

    public static async Task Main()
    {
        const int MAX_RETRIES = 30;

        Initialize();
        
        var factory = new ConnectionFactory()
        {
            HostName = "rabbitmq",
            Port = 5672,
            UserName = "rabbitun",
            Password = "rabbitpw"
        };

        IConnection? connection = null;
        
        int retryCount = 0;
        while (retryCount++ < MAX_RETRIES && !_sigintReceived)
        {
            try
            {
                connection = factory.CreateConnection();
                break;
            }
            catch (BrokerUnreachableException)
            {
                Console.WriteLine($"Could not connect to broker! Retrying ({retryCount}/{MAX_RETRIES})!");
                Thread.Sleep(2000);
            }
        }
        if (connection == null)
        {
            Console.WriteLine("Could not connect to broker! Exiting!");
            return;
        }
        Console.WriteLine("Connected to broker!");

        var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: "hello",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) => 
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine($" [x] Received {message}");
        };
        channel.BasicConsume(
            queue: "hello",
            autoAck: true,
            consumer: consumer);

        while (!_sigintReceived)
        {
            // Do nothing
        }

        try
        {
            channel.Close();
            channel.Dispose();
            connection.Close();
            connection.Dispose();
        }
        catch(Exception ex)
        {
            Console.WriteLine("Error while closing channel and connection!\n" + ex.Message);
        }

        Cleanup();

        await _tcs.Task;
        Console.WriteLine("Good bye!");
    }
}

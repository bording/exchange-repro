using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var maxConcurrency = Environment.ProcessorCount;

var factory = new ConnectionFactory();

factory.ConsumerDispatchConcurrency = maxConcurrency;
factory.DispatchConsumersAsync = true;
factory.UseBackgroundThreadsForIO = true;

using var connection = factory.CreateConnection("main");
using var channel = connection.CreateModel();

var arguments = new Dictionary<string, object> { { "x-queue-type", "classic" } };
//var arguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };

channel.QueueDeclare("receiver", true, false, false, arguments);

var message1 = "message1";
var message2 = "message2";

channel.ExchangeDeclare(message1, ExchangeType.Fanout, true);
channel.ExchangeDeclare(message2, ExchangeType.Fanout, true);

var numberofSubscribers = 2;

for (int i = 0; i < numberofSubscribers; i++)
{
    var queueName = $"subscriber{i}";

    channel.QueueDeclare(queueName, true, false, false, arguments);
    channel.ExchangeDeclare(queueName, ExchangeType.Fanout, true);
    channel.QueueBind(queueName, queueName, string.Empty);

    if (i == 0)
    {
        channel.ExchangeBind(queueName, message1, string.Empty);
    }

    channel.ExchangeBind(queueName, message2, string.Empty);
}

using var publishConnection = factory.CreateConnection("publish");
using var publishChannel = publishConnection.CreateModel();
publishChannel.ConfirmSelect();

for (int i = 0; i < 10_000; i++)
{
    var properties = channel.CreateBasicProperties();
    publishChannel.BasicPublish(string.Empty, "receiver", properties, null);
}

publishChannel.WaitForConfirmsOrDie();

Console.WriteLine("Press any key to start consuming messages");
Console.ReadKey();

var prefetchCount = (ushort)(maxConcurrency * 3);

channel.BasicQos(0, prefetchCount, false);

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.Received += Consumer_Received;

channel.BasicConsume("receiver", false, consumer);

Console.WriteLine("Press any key to quit");
Console.ReadKey();

Task Consumer_Received(object sender, BasicDeliverEventArgs basicDeliverEventArgs)
{
    var consumer = (AsyncEventingBasicConsumer)sender;
    var channel = consumer.Model;

    using var publishChannel = publishConnection.CreateModel();
    publishChannel.ConfirmSelect();

    var properties = channel.CreateBasicProperties();
    publishChannel.BasicPublish(message1, string.Empty, properties, null);

    properties = channel.CreateBasicProperties();
    publishChannel.BasicPublish(message2, string.Empty, properties, null);

    properties = channel.CreateBasicProperties();
    publishChannel.BasicPublish(message1, string.Empty, properties, null);

    properties = channel.CreateBasicProperties();
    publishChannel.BasicPublish(message2, string.Empty, properties, null);

    publishChannel.WaitForConfirms();

    channel.BasicAck(basicDeliverEventArgs.DeliveryTag, false);

    return Task.CompletedTask;
}
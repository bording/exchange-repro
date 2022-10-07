using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var factory = new ConnectionFactory();

using var mainConnection = factory.CreateConnection("main");
using var mainChannel = mainConnection.CreateModel();

var arguments = new Dictionary<string, object> { { "x-queue-type", "classic" } };
//var arguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };

mainChannel.QueueDeclare("receiver", true, false, false, arguments);

var message1 = "Test.ServiceBus.Messages:MessagesReceivedEvent1";
var message2 = "Test.ServiceBus.Messages:MessagesReceivedEvent2";
var messageBase = "Test.ServiceBus.Messages:MessageBase";
var eventMarker = "NServiceBus:IEvent";
var systemObject = "System:Object";

mainChannel.ExchangeDeclare(message1, ExchangeType.Fanout, true);
mainChannel.ExchangeDeclare(message2, ExchangeType.Fanout, true);
mainChannel.ExchangeDeclare(messageBase, ExchangeType.Fanout, true);
mainChannel.ExchangeDeclare(eventMarker, ExchangeType.Fanout, true);
mainChannel.ExchangeDeclare(systemObject, ExchangeType.Fanout, true);

mainChannel.ExchangeBind(messageBase, message1, string.Empty);
mainChannel.ExchangeBind(messageBase, message2, string.Empty);

mainChannel.ExchangeBind(eventMarker, message1, string.Empty);
mainChannel.ExchangeBind(eventMarker, message2, string.Empty);

mainChannel.ExchangeBind(systemObject, messageBase, string.Empty);

var numberofSubscribers = 10;

for (int i = 0; i < numberofSubscribers; i++)
{
    var queueName = $"subscriber{i:D2}";

    mainChannel.QueueDeclare(queueName, true, false, false, arguments);
    mainChannel.ExchangeDeclare(queueName, ExchangeType.Fanout, true);
    mainChannel.QueueBind(queueName, queueName, string.Empty);

    if (i == 0)
    {
        mainChannel.ExchangeBind(queueName, message1, string.Empty);
    }

    mainChannel.ExchangeBind(queueName, message2, string.Empty);
}

using var publishConnection = factory.CreateConnection("publish");
using var publishChannel = publishConnection.CreateModel();
publishChannel.ConfirmSelect();

for (int i = 0; i < 10_000; i++)
{
    var properties = publishChannel.CreateBasicProperties();
    publishChannel.BasicPublish(string.Empty, "receiver", properties, null);
}

publishChannel.WaitForConfirms();

Console.WriteLine("Press any key to start consuming messages");
Console.ReadKey();

mainChannel.BasicQos(0, 3, false);

var consumer = new EventingBasicConsumer(mainChannel);
consumer.Received += Consumer_Received;

mainChannel.BasicConsume("receiver", false, consumer);

Console.WriteLine("Press any key to quit");
Console.ReadKey();

void Consumer_Received(object? sender, BasicDeliverEventArgs basicDeliverEventArgs)
{
    for (int i = 0; i < 2; i++)
    {
        var properties = publishChannel.CreateBasicProperties();
        publishChannel.BasicPublish(message1, string.Empty, properties, null);
        publishChannel.BasicPublish(message2, string.Empty, properties, null);
    }

    publishChannel.WaitForConfirms();

    mainChannel.BasicAck(basicDeliverEventArgs.DeliveryTag, false);
}
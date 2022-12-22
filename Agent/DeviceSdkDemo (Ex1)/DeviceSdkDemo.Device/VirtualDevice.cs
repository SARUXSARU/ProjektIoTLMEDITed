using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System.Net.Mime;
using System.Text;
using Opc.UaFx;
using Opc.UaFx.Client;
//using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System;
using System.Diagnostics.CodeAnalysis;

namespace DeviceSdkDemo.Device
{
    public class VirtualDevice
    {
        private readonly DeviceClient client;

        public VirtualDevice(DeviceClient deviceClient)
        {
            this.client = deviceClient;
        }

        #region Sending Messages

        public async Task SendMessages(dynamic data)
        {
            var rnd = new Random();

            Console.WriteLine($"Device sending messages to IoTHub...\n");
            Console.WriteLine(data);
            var dataString = JsonConvert.SerializeObject(data);

            Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
            eventMessage.ContentType = MediaTypeNames.Application.Json;
            eventMessage.ContentEncoding = "utf-8";


            Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}, Data: [{dataString}]");

            await client.SendEventAsync(eventMessage);
        }



        #endregion Sending Messages

        #region Device Twin

        public async Task Twin(dynamic deviceErrors, dynamic productionRate)
        {
            var twin = await client.GetTwinAsync();

            Console.WriteLine($"\nInitial twin value received: \n{JsonConvert.SerializeObject(twin, Formatting.Indented)}");
            Console.WriteLine();

            var reportedProperties = new TwinCollection();
            reportedProperties["DeviceError"] = deviceErrors;

            var reportedProperties2 = new TwinCollection();
            reportedProperties2["ProductionRate"] = productionRate;


            await client.UpdateReportedPropertiesAsync(reportedProperties);
            await client.UpdateReportedPropertiesAsync(reportedProperties2);
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredproperties, object usercontext)
        {
            Console.WriteLine($"\tdesired property change:\n\t{JsonConvert.SerializeObject(desiredproperties)}");
            Console.WriteLine("\tsending current time as reported property");
            TwinCollection reportedproperties = new TwinCollection();
            reportedproperties["datetimelastdesiredpropertychangereceived"] = DateTime.Now;

            await client.UpdateReportedPropertiesAsync(reportedproperties).ConfigureAwait(false);
        }

        #endregion Device Twin

        #region Receiving Messages

        private async Task OnC2dMessageReceivedAsync(Message receivedMessage, object _)
        {
            Console.WriteLine($"\t{DateTime.Now}> C2D message callback - message received with Id={receivedMessage.MessageId}.");
            PrintMessage(receivedMessage);

            await client.CompleteAsync(receivedMessage);
            Console.WriteLine($"\t{DateTime.Now}> Completed C2D message with Id={receivedMessage.MessageId}.");

            receivedMessage.Dispose();
        }

        private void PrintMessage(Message receivedMessage)
        {
            string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
            Console.WriteLine($"\t\tReceived message: {messageData}");

            int propCount = 0;
            foreach (var prop in receivedMessage.Properties)
            {
                Console.WriteLine($"\t\tProperty[{propCount++}> Key={prop.Key} : Value={prop.Value}");
            }
        }

        #endregion Receiving Messages

        #region Direct Methods

        private async Task<MethodResponse> SendMessagesHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");

            var payload = JsonConvert.DeserializeAnonymousType(methodRequest.DataAsJson, new { data = default(dynamic) });

            await SendMessages(payload.data);

            return new MethodResponse(0);
        }

        private static async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");

            await Task.Delay(1000);

            return new MethodResponse(0);
        }

        #endregion Direct Methods
        public async Task initializehandlers()
        {
            await client.SetReceiveMessageHandlerAsync(OnC2dMessageReceivedAsync, client);

            await client.SetMethodHandlerAsync("sendmessages", SendMessagesHandler, client);
            await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);

            await client.SetDesiredPropertyUpdateCallback(OnDesiredPropertyChanged, client);
        }
    }
}


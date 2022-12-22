using DeviceSdkDemo.Device;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Net.Mime;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

internal class Program
{
    private static async Task Main(string[] args)
    {

        string path = Directory.GetCurrentDirectory() + @"\Linki.txt"; // odczyt danych z pliku .txt wymaganych do połączenia z IotHub oraz serwerem maszyny
        string[] FileContents = File.ReadAllLines(path);
        string OpcConnectionString = FileContents[3];
        string deviceConnectionString = FileContents[1];
        //string deviceConnectionString = "HostName=IOT-HUBzzzz.azure-devices.net;DeviceId=22.10.13;SharedAccessKey=e5U+h/asb5j23T3fGB0SwsWu2pwWkULCS8NEv4ymCpo=";
        using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);
        await deviceClient.OpenAsync();
        var device = new VirtualDevice(deviceClient);

        Console.WriteLine($"Connection to IoT success!");

        using (var client = new OpcClient(OpcConnectionString))
        {
            try      // zabezpieczenie przed błędnie podanym OpcConnectionString lub w sytuacji gdy połączenie nie może zosatć nawiązane  ( program wyświetli odpowiednie komunikaty gdy się połączy lub nie
            {
                client.Connect();
                Console.WriteLine("connection to devices succesed");
                var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (await periodicTimer.WaitForNextTickAsync())   // pobieranie danych z maszyny co sekundę
                {
                    var node = client.BrowseNode(OpcObjectTypes.ObjectsFolder);        //   ## wyszukiwanie nazwy aktualnie działających device
                    //Console.WriteLine(node);
                    foreach (var childNode in node.Children())
                    {
                        if ($"{childNode.DisplayName.Value}" != "Server")
                        {    //Console.WriteLine(childNode.DisplayName.Value);
                            int x = childNode.DisplayName.Value.Length;
                            string deviceName = childNode.DisplayName.Value;
                            string deviceID = "";
                            for (int y = 7; y < x; y++)
                            {
                                deviceID = deviceID + deviceName[y];
                            }
                            int.TryParse(deviceID, out int k);                      // ##

                            int ProductionStatus = (int)client.ReadNode($"ns=2;s=Device {k}/ProductionStatus").Value;   // ### przygotowanie danych do wysłania jako danych telemetrycznych
                            string WorkorderId = (string)client.ReadNode($"ns=2;s=Device {k}/WorkorderId").Value;
                            long GoodCount = (long)client.ReadNode($"ns=2;s=Device {k}/GoodCount").Value;
                            long BadCount = (long)client.ReadNode($"ns=2;s=Device {k}/BadCount").Value;
                            double Temperature = (double)client.ReadNode($"ns=2;s=Device {k}/Temperature").Value;
                            //Console.WriteLine(deviceName);
                            dynamic data = new
                            {
                                //DeviceID =  deviceName,
                                ProductionStatus,
                                WorkorderId,
                                GoodCount,
                                BadCount,
                                Temperature
                            };                                                                                         // ### 
                            //Console.WriteLine(data.Temperature);

                            await device.SendMessages(data);    // wysłanie danych telemetrycznych

                            int deviceErrors = (int)client.ReadNode($"ns=2;s=Device {k}/DeviceError").Value;            // #### Przygotowanie danych do przesłania reported deviceTwin
                            int productionRate = (int)client.ReadNode($"ns=2;s=Device {k}/ProductionRate").Value;       // ####

                            await device.Twin(deviceErrors, productionRate);                                            // Przesłanie danych reported deviceTwin

                            await device.initializehandlers();                                                         // możliwość wysyłania wiadomości lub directMethod do agenta

                        }
                    }
                }
            }
            catch   // wychwycenie sytuacji gdy nie można nawiązać połączenia z serverm OpcConnectionString
            {
                Console.WriteLine("connection devices fault");
            }
            client.Disconnect();
        }
        Console.ReadLine();
    }
}


using Client;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class GuestClient
{
    public static void Main() // Files are receiving
    {
        ConfigVM config = LoadConfiguration("/home/chaithanya/Documents/SMBShared/config.json");

        //TcpClient client = new TcpClient("192.168.0.176", 5000); // Use the host's IP
        TcpClient client = new TcpClient(config.IPAddress, config.Port);
        NetworkStream stream = client.GetStream();
        Console.WriteLine("Connected to server.");

        // Send heartbeats and wait for ACK
        SendHeartbeats(stream);

        // Request file data after receiving ACK
        byte[] fileDataMarker = Encoding.ASCII.GetBytes("FILEDATA");
        stream.Write(fileDataMarker, 0, fileDataMarker.Length);
        Console.WriteLine("FILEDATA marker sent to request file transfer.");

        // Proceed to read the files
        ReceiveFiles(stream, config);

        // Start sending heartbeats after file transfer
        MonitorConnection(stream, config);

        client.Close();
    }

    private static void SendHeartbeats(NetworkStream stream)
    {
        const string heartbeat = "HEARTBEAT";
        byte[] heartbeatBytes = Encoding.ASCII.GetBytes(heartbeat);
        stream.Write(heartbeatBytes, 0, heartbeatBytes.Length);
        Console.WriteLine("Heartbeat sent. Waiting for ACK...");

        // Wait for ACK response
        byte[] ackBuffer = new byte[3]; // "ACK" is 3 bytes
        int bytesRead = stream.Read(ackBuffer, 0, ackBuffer.Length);
        string ackResponse = Encoding.ASCII.GetString(ackBuffer, 0, bytesRead);

        if (ackResponse == "ACK")
        {
            Console.WriteLine("ACK received.");
        }
        else
        {
            Console.WriteLine("No ACK received.");
        }
    }

    private static void ReceiveFiles(NetworkStream stream, ConfigVM config)
    {
        // Read the marker for file data
        byte[] markerBuffer = new byte[8];
        stream.Read(markerBuffer, 0, markerBuffer.Length);
        string marker = Encoding.ASCII.GetString(markerBuffer);
        if (marker != "FILEDATA")
        {
            Console.WriteLine("Unknown marker received.");
            return;
        }

        // Read the number of files
        byte[] fileCountBytes = new byte[4];
        stream.Read(fileCountBytes, 0, fileCountBytes.Length);
        int fileCount = BitConverter.ToInt32(fileCountBytes, 0);
        Console.WriteLine($"Receiving {fileCount} files...");

        string destinationFolder = config.FilePath; //"/home/chaithanya/Documents/TCP"; // Destination on Linux
        Directory.CreateDirectory(destinationFolder); // Ensure the destination exists

        for (int i = 0; i < fileCount; i++)
        {
            // Receive the relative path
            byte[] relativePathLengthBytes = new byte[4];
            stream.Read(relativePathLengthBytes, 0, relativePathLengthBytes.Length);
            int relativePathLength = BitConverter.ToInt32(relativePathLengthBytes, 0);

            byte[] relativePathBytes = new byte[relativePathLength];
            stream.Read(relativePathBytes, 0, relativePathBytes.Length);
            string relativePath = Encoding.ASCII.GetString(relativePathBytes);
            string fullPath = Path.Combine(destinationFolder, relativePath.Replace('\\', '/'));

            // Create directories as needed
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // Receive the file length
            byte[] fileLengthBytes = new byte[8]; // long = 8 bytes
            stream.Read(fileLengthBytes, 0, fileLengthBytes.Length);
            long fileLength = BitConverter.ToInt64(fileLengthBytes, 0);

            // Receive the file in chunks and write to disk
            const int chunkSize = 1024 * 64; // 64 KB chunks
            byte[] buffer = new byte[chunkSize];
            long totalBytesReceived = 0;

            using (FileStream fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                while (totalBytesReceived < fileLength)
                {
                    int bytesToRead = (int)Math.Min(chunkSize, fileLength - totalBytesReceived);
                    int bytesRead = stream.Read(buffer, 0, bytesToRead);
                    fileStream.Write(buffer, 0, bytesRead);
                    totalBytesReceived += bytesRead;
                }
            }

            Console.WriteLine($"Received file: {fullPath}");
        }
    }

    private static void MonitorConnection(NetworkStream stream, ConfigVM config)
    {
        const string heartbeat = "HEARTBEAT";
        byte[] heartbeatBytes = Encoding.ASCII.GetBytes(heartbeat);
        int attempts = config.RetryCount;// 5;

        while (true) // Loop to keep sending heartbeats
        {
            // Send heartbeat
            stream.Write(heartbeatBytes, 0, heartbeatBytes.Length);
            Console.WriteLine("Heartbeat sent. Waiting for ACK...");

            // Wait for ACK response
            byte[] ackBuffer = new byte[3]; // "ACK" is 3 bytes
            int bytesRead = stream.Read(ackBuffer, 0, ackBuffer.Length);
            string ackResponse = Encoding.ASCII.GetString(ackBuffer, 0, bytesRead);

            if (ackResponse == "ACK")
            {
                Console.WriteLine("ACK received.");
            }
            else
            {
                Console.WriteLine("No ACK received. Connection might be lost.");
            }

            Thread.Sleep(5000); // Wait for 5 seconds before sending the next heartbeat
        }
    }

    public static ConfigVM LoadConfiguration(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Configuration file not found.", filePath);
        }

        string json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<ConfigVM>(json);
    }
}

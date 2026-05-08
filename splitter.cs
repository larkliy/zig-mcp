
const int ChunkSize = 100_000;

if (args.Length != 1)
{
    Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} input_file");
    return;
}

string inputFile = args[0];
if (!File.Exists(inputFile))
{
    Console.WriteLine($"Error: file '{inputFile}' not found.");
    return;
}

Directory.CreateDirectory("split");

string baseName = Path.GetFileNameWithoutExtension(inputFile);
int counter = 1;
long fileSize = new FileInfo(inputFile).Length;
Console.WriteLine($"File size: {fileSize} bytes");

var buffer = new byte[ChunkSize];
using FileStream inStream = File.OpenRead(inputFile);
int bytesRead;
while ((bytesRead = inStream.Read(buffer, 0, ChunkSize)) > 0)
{
    string outputFile = Path.Combine("split", $"{baseName}_part_{counter}.md");

    using (FileStream outStream = File.Create(outputFile))
    using (StreamWriter writer = new(outStream))
    {
        writer.WriteLine("```");
        writer.Flush();

        outStream.Write(buffer, 0, bytesRead);
        outStream.Flush();

        writer.WriteLine();
        writer.WriteLine("```");
    }

    Console.WriteLine($"Created {outputFile}");
    counter++;
}

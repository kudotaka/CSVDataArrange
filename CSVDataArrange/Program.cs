using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Cysharp.Text;
using ZLogger;
using CsvHelper.Configuration;
using System.Globalization;
using CsvHelper;
using System.Text;


var builder = ConsoleApp.CreateBuilder(args);
builder.ConfigureServices((ctx,services) =>
{
    // Register appconfig.json to IOption<MyConfig>
    services.Configure<MyConfig>(ctx.Configuration);

    // Using Cysharp/ZLogger for logging to file
    services.AddLogging(logging =>
    {
        logging.ClearProviders(); // clear all providers
        logging.SetMinimumLevel(LogLevel.Debug); // change log level if you want

        logging.AddZLoggerRollingFile(
                (dt, x) => $"logs/{dt.ToLocalTime():yyyy-MM-dd}_{x:000}.log", x => x.ToLocalTime().Date, 1024, options => { options.EnableStructuredLogging = true;
            });

        logging.AddZLoggerConsole(
            options => {
                options.PrefixFormatter = (writer, info) => ZString.Utf8Format(writer, "[{0}][{1}]", info.Timestamp.DateTime.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss"), info.LogLevel);
            });
    });
});

var app = builder.Build();
app.AddAllCommandType();

// some argument from DI.
app.AddRootCommand((ConsoleAppContext ctx, IOptions<MyConfig> config) => { });
app.Run();


public class MyConfig
{
    public UInt32 ColumnCount {get; set;} = 0;
    public string HeaderWord {get; set;} = "DEFAULT";
    public string InputCsvFile {get; set;} = "DEFAULT";
    public string InputCsvDelimiter {get; set;} = "DEFAULT";
    public string InputCsvEncoding {get; set;} = "DEFAULT";
    public string InputCsvNewLine {get; set;} = "DEFAULT";
    public string InputCsvHasHeader {get; set;} = "DEFAULT";
    public string OutputCsvFile {get; set;} = "DEFAULT";
    public string OutputCsvDelimiter {get; set;} = "DEFAULT";
    public string OutputCsvEncoding {get; set;} = "DEFAULT";
    public string OutputCsvNewLine {get; set;} = "DEFAULT";
    public string OutputCsvHasHeader {get; set;} = "DEFAULT";
}

[Command("csv")]
public class CsvApp : ConsoleAppBase
{
    readonly ILogger<CsvApp> logger;
    readonly IOptions<MyConfig> config;

    public CsvApp(ILogger<CsvApp> logger,IOptions<MyConfig> config)
    {
        this.logger = logger;
        this.config = config;
    }

    [Command("arrange")]
    public int Arrange()
    {
        logger.ZLogDebug("start arrange()");

        bool bInputCsvHasHeader = false;
        var encodeingInput = Encoding.UTF8; // default encodeing
        var encodeingOutput = Encoding.UTF8; // default encodeing

        UInt32 columnCount = config.Value.ColumnCount;
        string headerWord = config.Value.HeaderWord;
        string inputCsvFile = config.Value.InputCsvFile;
        string inputCsvDelimiter = config.Value.InputCsvDelimiter;
        string inputCsvEncoding = config.Value.InputCsvEncoding;
        string inputCsvNewLine = config.Value.InputCsvNewLine;
        string inputCsvHasHeader = config.Value.InputCsvHasHeader;
        string outputCsvFile = config.Value.OutputCsvFile;
        string outputCsvDelimiter = config.Value.OutputCsvDelimiter;
        string outputCsvEncoding = config.Value.OutputCsvEncoding;
        string outputCsvNewLine = config.Value.OutputCsvNewLine;

        if (columnCount == 0)
        {
            logger.ZLogError("ColumnCount is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (String.IsNullOrEmpty(headerWord) || headerWord.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("HeaderWord is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (String.IsNullOrEmpty(inputCsvFile) || inputCsvFile.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("InputCsvFile is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (String.IsNullOrEmpty(inputCsvDelimiter) || inputCsvDelimiter.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("InputCsvDelimiter is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (String.IsNullOrEmpty(inputCsvEncoding) || inputCsvEncoding.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("InputCsvEncoding is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (String.IsNullOrEmpty(inputCsvNewLine) || inputCsvNewLine.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("InputCsvNewLine is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (String.IsNullOrEmpty(inputCsvHasHeader) || inputCsvHasHeader.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("InputCsvHasHeader is empty/default value. Check to appsettings.json.");
            return 1;
        }

        if (String.IsNullOrEmpty(outputCsvFile) || outputCsvFile.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("OutputCsvFile is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (String.IsNullOrEmpty(outputCsvDelimiter) || outputCsvDelimiter.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("OutputCsvDelimiter is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (String.IsNullOrEmpty(outputCsvEncoding) || outputCsvEncoding.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("OutputCsvEncoding is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (String.IsNullOrEmpty(outputCsvNewLine) || outputCsvNewLine.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("OutputCsvNewLine is empty/default value. Check to appsettings.json.");
            return 1;
        }

        try
        {
            bInputCsvHasHeader = bool.Parse(inputCsvHasHeader);
        }
        catch (System.Exception)
        {
            logger.ZLogError("InputCsvHasHeader is not Parse. Check to appsettings.json.");
            return 1;
        }
        if (string.Equals("Shift_JIS", inputCsvEncoding))
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            encodeingInput = Encoding.GetEncoding("Shift_JIS");
        }
        if (string.Equals("Shift_JIS", outputCsvEncoding))
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            encodeingOutput = Encoding.GetEncoding("Shift_JIS");
        }

        List<Dictionary<string, string>> tempCsv = new List<Dictionary<string, string>>();
        var readCsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = bInputCsvHasHeader,
            Delimiter = inputCsvDelimiter,
            NewLine = inputCsvNewLine,
            Encoding = encodeingInput,
            DetectColumnCountChanges = false,
        };
        using (var csvStreamReader = new StreamReader(inputCsvFile))
        {
            using (var csvReader = new CsvReader(csvStreamReader, readCsvConfig))
            {
                if (bInputCsvHasHeader)
                {
                    csvReader.Read();
                    csvReader.ReadHeader();
                }
                while (csvReader.Read())
                {
                    Dictionary<string, string> tempData = new Dictionary<string, string>();
                    var index1 = csvReader.GetField(1);
                    logger.ZLogDebug("index1:{0}", index1);
                    switch (index1)
                    {
                        case "DATA":
                            logger.ZLogDebug("index1 is DATA");
                            for (int i = 0; i < columnCount; i++)
                            {
                                var tempValue = csvReader.GetField(i);
                                if (string.IsNullOrEmpty(tempValue))
                                {
                                    tempValue = "";
                                }
                                tempData.Add(i.ToString(), tempValue);
                            }
                            tempCsv.Add(tempData);
                            break;
                        case "TOP":
                            logger.ZLogDebug("index1 is TOP");
                            break;
                        case "END":
                            logger.ZLogDebug("index1 is END");
                            break;
                        default:
                            logger.ZLogDebug("index1 is ?");
                            break;
                    }
                }
            }
        }


        var writeCsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            Delimiter = outputCsvDelimiter,
            NewLine = outputCsvNewLine,
            Encoding = encodeingOutput,
        };
        using (var stream = File.OpenWrite(outputCsvFile))
        using (var writerWriter = new StreamWriter(stream, Encoding.UTF8))
        {
            // header
            writerWriter.NewLine = outputCsvNewLine;
            writerWriter.WriteLine(headerWord);

            // data
            using (var csvWriter = new CsvWriter(writerWriter, writeCsvConfig))
            {
                foreach (var dic in tempCsv)
                {
                    for (int i = 0; i < columnCount; i++)
                    {
                        csvWriter.WriteField(dic[i.ToString()]);
                    }
                    csvWriter.NextRecord();
                }
            }
        }

        logger.ZLogInformation("FILE:{0} is success.", inputCsvFile);
        return 0;
    }
}

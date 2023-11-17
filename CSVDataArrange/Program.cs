using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Cysharp.Text;
using ZLogger;
using CsvHelper.Configuration;
using System.Globalization;
using CsvHelper;
using System.Text;
using System.Text.RegularExpressions;


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
    public string ArrangeMode {get; set;} = "DEFAULT";
    public string ArrangeWord {get; set;} = "DEFAULT";
    public int ArrangeIndex {get; set;} = -1;
    public string ExtractIndexs {get; set;} = "DEFAULT";
    public UInt32 ColumnCount {get; set;} = 0;
    public string InputCsvDelimiter {get; set;} = "DEFAULT";
    public string InputCsvEncoding {get; set;} = "DEFAULT";
    public string InputCsvNewLine {get; set;} = "DEFAULT";
    public string InputCsvHasHeader {get; set;} = "DEFAULT";
    public string OutputCsvDelimiter {get; set;} = "DEFAULT";
    public string OutputCsvEncoding {get; set;} = "DEFAULT";
    public string OutputCsvNewLine {get; set;} = "DEFAULT";
    public string OutputCsvHasHeader {get; set;} = "DEFAULT";
    public string OutputCsvHeaderWord {get; set;} = "DEFAULT";
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
    public int Arrange(string inputfile, string inputfiletemp, string outputfile)
    {
        logger.ZLogDebug("start arrange()");

        if (string.IsNullOrEmpty(inputfile) || string.IsNullOrEmpty(inputfiletemp) || string.IsNullOrEmpty(outputfile))
        {
            logger.ZLogError($"Error: arg is NullOrEmpty.");
            return 1;
        }
        string inputCsvFile = inputfile;
        string inputCsvFileTemp = inputfiletemp;
        string outputCsvFile = outputfile;

        bool bInputCsvHasHeader = false;
        var encodeingInput = Encoding.UTF8; // default encodeing
        var encodeingOutput = Encoding.UTF8; // default encodeing

        string arrangeMode = config.Value.ArrangeMode;
        string arrangeWord = config.Value.ArrangeWord;
        int arrangeIndex = config.Value.ArrangeIndex;
        UInt32 columnCount = config.Value.ColumnCount;
        string inputCsvDelimiter = config.Value.InputCsvDelimiter;
        string inputCsvEncoding = config.Value.InputCsvEncoding;
        string inputCsvNewLine = config.Value.InputCsvNewLine;
        string inputCsvHasHeader = config.Value.InputCsvHasHeader;
        string outputCsvDelimiter = config.Value.OutputCsvDelimiter;
        string outputCsvEncoding = config.Value.OutputCsvEncoding;
        string outputCsvNewLine = config.Value.OutputCsvNewLine;
        string outputCsvHeaderWord = config.Value.OutputCsvHeaderWord;

        if (String.IsNullOrEmpty(arrangeMode) || arrangeMode.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("ArrangeMode is empty/default value. Check to appsettings.json.");
            return 1;
        }
        else if ( arrangeMode.CompareTo("pass") != 0 && arrangeMode.CompareTo("reject") != 0
                && arrangeMode.CompareTo("regex-pass") != 0 && arrangeMode.CompareTo("regex-reject") != 0)
        {
            logger.ZLogError("ArrangeMode is not [pass]/[reject]/[regex-pass]/[regex-reject] value. Check to appsettings.json.");
            return 1;
        }
        if (String.IsNullOrEmpty(arrangeWord) || arrangeWord.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("ArrangeWord is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (arrangeIndex < 0)
        {
            logger.ZLogError("ArrangeIndex is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (columnCount == 0)
        {
            logger.ZLogError("ColumnCount is empty/default value. Check to appsettings.json.");
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
        if (String.IsNullOrEmpty(outputCsvHeaderWord) || outputCsvHeaderWord.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("HeaderWord is empty/default value. Check to appsettings.json.");
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

        string tempTextReplase = "";
        using (var textStreamReader = new StreamReader(inputCsvFile, encodeingInput))
        {
            var tempText = textStreamReader.ReadToEnd();
            tempTextReplase = tempText.Replace("\n", "\r\n").Replace("\r\r", "\r");
        }
        using (var textStreamWriter = new StreamWriter(inputCsvFileTemp, false, encodeingInput))
        {
            textStreamWriter.Write(tempTextReplase);
            textStreamWriter.Flush();
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
        using (var csvStreamReader = new StreamReader(inputCsvFileTemp))
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
                    var index1 = csvReader.GetField(arrangeIndex);
                    logger.ZLogDebug("index1:{0}", index1);
                    if (arrangeMode.CompareTo("pass") == 0)
                    {
                        // pass
                        if (arrangeWord.CompareTo(index1) == 0)
                        {
                            logger.ZLogDebug("[pass] index1 is {0}. Add!", index1);
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
                        }
                        else
                        {
                            logger.ZLogDebug("[pass] index1 is {0}. no Add.", index1);
                        }
                    }
                    else if (arrangeMode.CompareTo("reject") == 0)
                    {
                        // reject
                        if (arrangeWord.CompareTo(index1) != 0)
                        {
                            logger.ZLogDebug("[reject] index1 is {0}. Add!", index1);
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
                        }
                        else
                        {
                            logger.ZLogDebug("[reject] index1 is {0}. no Add.", index1);
                        }
                    }
                    else if (arrangeMode.CompareTo("regex-pass") == 0)
                    {
                        // regex-pass
                        if (String.IsNullOrEmpty(index1))
                        {
                            logger.ZLogDebug("[regex-pass] index1 is null/empty value. no Add.");
                        }
                        else if (Regex.IsMatch(index1, arrangeWord, RegexOptions.None))
                        {
                            logger.ZLogDebug("[regex-pass] index1 is {0}. Add!", index1);
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
                        }
                        else
                        {
                            logger.ZLogDebug("[regex-pass] index1 is {0}. no Add.", index1);
                        }
                    }
                    else if (arrangeMode.CompareTo("regex-reject") == 0)
                    {
                        // regex-reject
                        if (String.IsNullOrEmpty(index1))
                        {
                            logger.ZLogDebug("[regex-reject] index1 is null/empty value. no Add.");
                        }
                        else if (!Regex.IsMatch(index1, arrangeWord, RegexOptions.None))
                        {
                            logger.ZLogDebug("[regex-reject] index1 is {0}. Add!", index1);
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
                        }
                        else
                        {
                            logger.ZLogDebug("[regex-reject] index1 is {0}. no Add.", index1);
                        }
                    }
                 }
            }
        }


        if (File.Exists(outputCsvFile))
        {
            File.Delete(outputCsvFile);
        }
        var writeCsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            Delimiter = outputCsvDelimiter,
            NewLine = outputCsvNewLine,
            Encoding = encodeingOutput,
            ShouldQuote = x => true,
        };
        using (var stream = File.OpenWrite(outputCsvFile))
        using (var writerWriter = new StreamWriter(stream, Encoding.UTF8))
        {
            // header
            writerWriter.NewLine = outputCsvNewLine;
            writerWriter.WriteLine(outputCsvHeaderWord);

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

    [Command("extract")]
    public int Extract(string inputfile, string inputfiletemp, string outputfile)
    {
        logger.ZLogDebug("start extract()");

        if (string.IsNullOrEmpty(inputfile) || string.IsNullOrEmpty(inputfiletemp) || string.IsNullOrEmpty(outputfile))
        {
            logger.ZLogError($"Error: arg is NullOrEmpty.");
            return 1;
        }
        string inputCsvFile = inputfile;
        string inputCsvFileTemp = inputfiletemp;
        string outputCsvFile = outputfile;

        bool bInputCsvHasHeader = false;
        var encodeingInput = Encoding.UTF8; // default encodeing
        var encodeingOutput = Encoding.UTF8; // default encodeing

        string extractIndexs = config.Value.ExtractIndexs;
        UInt32 columnCount = config.Value.ColumnCount;
        string inputCsvDelimiter = config.Value.InputCsvDelimiter;
        string inputCsvEncoding = config.Value.InputCsvEncoding;
        string inputCsvNewLine = config.Value.InputCsvNewLine;
        string inputCsvHasHeader = config.Value.InputCsvHasHeader;
        string outputCsvDelimiter = config.Value.OutputCsvDelimiter;
        string outputCsvEncoding = config.Value.OutputCsvEncoding;
        string outputCsvNewLine = config.Value.OutputCsvNewLine;
        string outputCsvHeaderWord = config.Value.OutputCsvHeaderWord;

        if (String.IsNullOrEmpty(extractIndexs) || extractIndexs.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("ExtractIndexs is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (columnCount == 0)
        {
            logger.ZLogError("ColumnCount is empty/default value. Check to appsettings.json.");
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
        if (String.IsNullOrEmpty(outputCsvHeaderWord) || outputCsvHeaderWord.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("HeaderWord is empty/default value. Check to appsettings.json.");
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

        string tempTextReplase = "";
        using (var textStreamReader = new StreamReader(inputCsvFile, encodeingInput))
        {
            var tempText = textStreamReader.ReadToEnd();
            tempTextReplase = tempText.Replace("\n", "\r\n").Replace("\r\r", "\r");
        }
        using (var textStreamWriter = new StreamWriter(inputCsvFileTemp, false, encodeingInput))
        {
            textStreamWriter.Write(tempTextReplase);
            textStreamWriter.Flush();
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
        using (var csvStreamReader = new StreamReader(inputCsvFileTemp))
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
                    var array1 = extractIndexs.Split(',');
                    Dictionary<string, string> tempData = new Dictionary<string, string>();
                    for (int i = 0; i < array1.Length; i++)
                    {
                        logger.ZLogDebug("{0}. Add!", array1[i]);
                        var tempValue = csvReader.GetField(int.Parse(array1[i]));
                        if (string.IsNullOrEmpty(tempValue))
                        {
                            tempValue = "";
                        }
                        tempData.Add(i.ToString(), tempValue);
                    }
                    tempCsv.Add(tempData);
                }
            }
        }


        if (File.Exists(outputCsvFile))
        {
            File.Delete(outputCsvFile);
        }
        var writeCsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            Delimiter = outputCsvDelimiter,
            NewLine = outputCsvNewLine,
            Encoding = encodeingOutput,
            ShouldQuote = x => true,
        };
        using (var stream = File.OpenWrite(outputCsvFile))
        using (var writerWriter = new StreamWriter(stream, Encoding.UTF8))
        {
            // header
            writerWriter.NewLine = outputCsvNewLine;
            writerWriter.WriteLine(outputCsvHeaderWord);

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

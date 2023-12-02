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
    public UInt32 ArrangeColumnCount {get; set;} = 0;
    public string Arrange2Mode {get; set;} = "DEFAULT";
    public string Arrange2Word {get; set;} = "DEFAULT";
    public int Arrange2Index {get; set;} = -1;
    public UInt32 Arrange2ColumnCount {get; set;} = 0;
    public string Arrange3Mode {get; set;} = "DEFAULT";
    public string Arrange3Word {get; set;} = "DEFAULT";
    public int Arrange3Index {get; set;} = -1;
    public UInt32 Arrange3ColumnCount {get; set;} = 0;
    public string ExtractIndexs {get; set;} = "DEFAULT";
    public UInt32 ExtractColumnCount {get; set;} = 0;
    public string OutputCsvHeaderWord {get; set;} = "DEFAULT";
}

public static class MyUtility
{
    public static string convertUtf8String(Encoding srcEncode, Encoding dstEncode, string targetString)
    {
        if (srcEncode != dstEncode)
        {
            byte[] targetStringBytes = srcEncode.GetBytes(targetString);
            byte[] convertUtf8Bytes = Encoding.Convert(srcEncode, dstEncode, targetStringBytes);
            return dstEncode.GetString(convertUtf8Bytes);
        }
        return targetString;
    }

    public static string selectArrangeWord(IOptions<MyConfig> myConfig, int arrangeTime)
    {
        switch (arrangeTime)
        {
            case 1:
                return myConfig.Value.ArrangeWord;
            case 2:
                return myConfig.Value.Arrange2Word;
            case 3:
                return myConfig.Value.Arrange3Word;
            default:
                return "DEFAULT";
        }
    }

    public static string selectArrangeMode(IOptions<MyConfig> myConfig, int arrangeTime)
    {
        switch (arrangeTime)
        {
            case 1:
                return myConfig.Value.ArrangeMode;
            case 2:
                return myConfig.Value.Arrange2Mode;
            case 3:
                return myConfig.Value.Arrange3Mode;
            default:
                return "DEFAULT";
        }
    }

    public static int selectArrangeIndex(IOptions<MyConfig> myConfig, int arrangeTime)
    {
        switch (arrangeTime)
        {
            case 1:
                return myConfig.Value.ArrangeIndex;
            case 2:
                return myConfig.Value.Arrange2Index;
            case 3:
                return myConfig.Value.Arrange3Index;
            default:
                return -1;
        }
    }

    public static UInt32 selectArrangeColumnCount(IOptions<MyConfig> myConfig, int arrangeTime)
    {
        switch (arrangeTime)
        {
            case 1:
                return myConfig.Value.ArrangeColumnCount;
            case 2:
                return myConfig.Value.Arrange2ColumnCount;
            case 3:
                return myConfig.Value.Arrange3ColumnCount;
            default:
                return 0;
        }
    }
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
    public int Arrange(string inputfile, string filetemp, string outputfile, string inputencoding, string inputhasheader, int arrangeTime = 1)
    {
        logger.ZLogDebug("start arrange()");

        if (string.IsNullOrEmpty(inputfile) || string.IsNullOrEmpty(filetemp)
        || string.IsNullOrEmpty(outputfile) || string.IsNullOrEmpty(inputencoding)
        || string.IsNullOrEmpty(inputhasheader))
        {
            logger.ZLogError($"Error: arg is NullOrEmpty.");
            return 1;
        }
        if (!(inputencoding.Equals("UTF-8") || inputencoding.Equals("Shift_JIS")))
        {
            logger.ZLogError($"Error: arg[inputencoding] is not [UTF-8]/[Shift_JIS] value.");
            return 1;
        }
        if (!(inputhasheader.Equals("true") || inputhasheader.Equals("false")))
        {
            logger.ZLogError($"Error: arg[inputhasheader] is not [true]/[false] value.");
            return 1;
        }
        string inputCsvFile = inputfile;
        string inputCsvFileTemp = filetemp;
        string outputCsvFile = outputfile;

        bool bInputCsvHasHeader = false;
        var encodeingInput = Encoding.UTF8; // default encodeing
        var encodeingOutput = Encoding.UTF8; // default encodeing

        string arrangeMode = MyUtility.selectArrangeMode(config, arrangeTime);
        string arrangeWord = MyUtility.selectArrangeWord(config, arrangeTime);
        int arrangeIndex = MyUtility.selectArrangeIndex(config, arrangeTime);
        UInt32 arrangeColumnCount = MyUtility.selectArrangeColumnCount(config, arrangeTime);
        string inputCsvDelimiter = ",";
        string inputCsvEncoding = inputencoding;
        string inputCsvNewLine = "\r\n";
        string inputCsvHasHeader = inputhasheader;
        string outputCsvDelimiter = ",";
        string outputCsvNewLine = "\r\n";
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
        if (arrangeColumnCount == 0)
        {
            logger.ZLogError("ArrangeColumnCount is empty/default value. Check to appsettings.json.");
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
            logger.ZLogError("InputCsvHasHeader is not Parse. Check to arg[inputhasheader].");
            return 1;
        }
        if (string.Equals("Shift_JIS", inputCsvEncoding))
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            encodeingInput = Encoding.GetEncoding("Shift_JIS");
        }
        logger.ZLogDebug("encodeingInput:{0}, encodeingOutput:{1}", encodeingInput.EncodingName, encodeingOutput.EncodingName);

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
        using (var csvStreamReader = new StreamReader(inputCsvFileTemp, encodeingInput))
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
                    if (!string.IsNullOrEmpty(index1))
                    {
                        index1 = MyUtility.convertUtf8String(encodeingInput, encodeingOutput, index1);
                    }
                    logger.ZLogDebug("index1:{0}", index1);

                    var array1 = arrangeWord.Split(',');
                    for (int j = 0; j < array1.Length; j++)
                    {
                        if (arrangeMode.CompareTo("pass") == 0)
                        {
                            // pass
                            if (array1[j].CompareTo(index1) == 0)
                            {
                                logger.ZLogDebug("[pass] index1 is {0}. ArrangeWord is {1}. Add!", index1, array1[j]);
                                for (int i = 0; i < arrangeColumnCount; i++)
                                {
                                    var tempValue = csvReader.GetField(i);
                                    if (string.IsNullOrEmpty(tempValue))
                                    {
                                        tempValue = "";
                                    }
                                    else
                                    {
                                        tempValue = MyUtility.convertUtf8String(encodeingInput, encodeingOutput, tempValue);
                                    }
                                    tempData.Add(i.ToString(), tempValue);
                                }
                                tempCsv.Add(tempData);
                            }
                            else
                            {
                                logger.ZLogDebug("[pass] index1 is {0}. ArrangeWord is {1}. no Add.", index1, array1[j]);
                            }
                        }
                        else if (arrangeMode.CompareTo("reject") == 0)
                        {
                            // reject
                            if (array1[j].CompareTo(index1) != 0)
                            {
                                logger.ZLogDebug("[reject] index1 is {0}. ArrangeWord is {1}. Add!", index1, array1[j]);
                                for (int i = 0; i < arrangeColumnCount; i++)
                                {
                                    var tempValue = csvReader.GetField(i);
                                    if (string.IsNullOrEmpty(tempValue))
                                    {
                                        tempValue = "";
                                    }
                                    else
                                    {
                                        tempValue = MyUtility.convertUtf8String(encodeingInput, encodeingOutput, tempValue);
                                    }
                                    tempData.Add(i.ToString(), tempValue);
                                }
                                tempCsv.Add(tempData);
                            }
                            else
                            {
                                logger.ZLogDebug("[reject] index1 is {0}. ArrangeWord is {1}. no Add.", index1, array1[j]);
                            }
                        }
                        else if (arrangeMode.CompareTo("regex-pass") == 0)
                        {
                            // regex-pass
                            if (String.IsNullOrEmpty(index1))
                            {
                                logger.ZLogDebug("[regex-pass] index1 is null/empty value. no Add.");
                            }
                            else if (Regex.IsMatch(index1, array1[j], RegexOptions.None))
                            {
                                logger.ZLogDebug("[regex-pass] index1 is {0}. ArrangeWord is {1}. Add!", index1, array1[j]);
                                for (int i = 0; i < arrangeColumnCount; i++)
                                {
                                    var tempValue = csvReader.GetField(i);
                                    if (string.IsNullOrEmpty(tempValue))
                                    {
                                        tempValue = "";
                                    }
                                    else
                                    {
                                        tempValue = MyUtility.convertUtf8String(encodeingInput, encodeingOutput, tempValue);
                                    }
                                    tempData.Add(i.ToString(), tempValue);
                                }
                                tempCsv.Add(tempData);
                            }
                            else
                            {
                                logger.ZLogDebug("[regex-pass] index1 is {0}. ArrangeWord is {1}. no Add.", index1, array1[j]);
                            }
                        }
                        else if (arrangeMode.CompareTo("regex-reject") == 0)
                        {
                            // regex-reject
                            if (String.IsNullOrEmpty(index1))
                            {
                                logger.ZLogDebug("[regex-reject] index1 is null/empty value. no Add.");
                            }
                            else if (!Regex.IsMatch(index1, array1[j], RegexOptions.None))
                            {
                                logger.ZLogDebug("[regex-reject] index1 is {0}. ArrangeWord is {1}. Add!", index1, array1[j]);
                                for (int i = 0; i < arrangeColumnCount; i++)
                                {
                                    var tempValue = csvReader.GetField(i);
                                    if (string.IsNullOrEmpty(tempValue))
                                    {
                                        tempValue = "";
                                    }
                                    else
                                    {
                                        tempValue = MyUtility.convertUtf8String(encodeingInput, encodeingOutput, tempValue);
                                    }
                                    tempData.Add(i.ToString(), tempValue);
                                }
                                tempCsv.Add(tempData);
                            }
                            else
                            {
                                logger.ZLogDebug("[regex-reject] index1 is {0}. ArrangeWord is {1}. no Add.", index1, array1[j]);
                            }
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
        using (var writerWriter = new StreamWriter(stream, encodeingOutput))
        {
            // header
            writerWriter.NewLine = outputCsvNewLine;
            writerWriter.WriteLine(outputCsvHeaderWord);

            // data
            using (var csvWriter = new CsvWriter(writerWriter, writeCsvConfig))
            {
                foreach (var dic in tempCsv)
                {
                    for (int i = 0; i < arrangeColumnCount; i++)
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
    public int Extract(string inputfile, string filetemp, string outputfile, string inputencoding, string inputhasheader)
    {
        logger.ZLogDebug("start extract()");

        if (string.IsNullOrEmpty(inputfile) || string.IsNullOrEmpty(filetemp)
         || string.IsNullOrEmpty(outputfile) || string.IsNullOrEmpty(inputencoding)
         || string.IsNullOrEmpty(inputhasheader))
        {
            logger.ZLogError($"Error: arg is NullOrEmpty.");
            return 1;
        }
        if (!(inputencoding.Equals("UTF-8") || inputencoding.Equals("Shift_JIS")))
        {
            logger.ZLogError($"Error: arg[inputencoding] is not [UTF-8]/[Shift_JIS] value.");
            return 1;
        }
        if (!(inputhasheader.Equals("true") || inputhasheader.Equals("false")))
        {
            logger.ZLogError($"Error: arg[inputhasheader] is not [true]/[false] value.");
            return 1;
        }
        string inputCsvFile = inputfile;
        string inputCsvFileTemp = filetemp;
        string outputCsvFile = outputfile;

        bool bInputCsvHasHeader = false;
        var encodeingInput = Encoding.UTF8; // default encodeing
        var encodeingOutput = Encoding.UTF8; // default encodeing

        string extractIndexs = config.Value.ExtractIndexs;
        UInt32 extractColumnCount = config.Value.ExtractColumnCount;
        string inputCsvDelimiter = ",";
        string inputCsvEncoding = inputencoding;
        string inputCsvNewLine = "\r\n";
        string inputCsvHasHeader = inputhasheader;
        string outputCsvDelimiter = ",";
        string outputCsvNewLine = "\r\n";
        string outputCsvHeaderWord = config.Value.OutputCsvHeaderWord;

        if (String.IsNullOrEmpty(extractIndexs) || extractIndexs.CompareTo("DEFAULT") == 0)
        {
            logger.ZLogError("ExtractIndexs is empty/default value. Check to appsettings.json.");
            return 1;
        }
        if (extractColumnCount == 0)
        {
            logger.ZLogError("ExtractColumnCount is empty/default value. Check to appsettings.json.");
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
            logger.ZLogError("InputCsvHasHeader is not Parse. Check to arg[inputhasheader].");
            return 1;
        }
        if (string.Equals("Shift_JIS", inputCsvEncoding))
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            encodeingInput = Encoding.GetEncoding("Shift_JIS");
        }
        logger.ZLogDebug("encodeingInput:{0}, encodeingOutput:{1}", encodeingInput.EncodingName, encodeingOutput.EncodingName);

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
        using (var csvStreamReader = new StreamReader(inputCsvFileTemp, encodeingInput))
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
                        else
                        {
                            tempValue = MyUtility.convertUtf8String(encodeingInput, encodeingOutput, tempValue);
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
        using (var writerWriter = new StreamWriter(stream, encodeingOutput))
        {
            // header
            writerWriter.NewLine = outputCsvNewLine;
            writerWriter.WriteLine(outputCsvHeaderWord);

            // data
            using (var csvWriter = new CsvWriter(writerWriter, writeCsvConfig))
            {
                foreach (var dic in tempCsv)
                {
                    for (int i = 0; i < extractColumnCount; i++)
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

    [Command("arrangeandextract")]
    public int ArrangeAndExtract(string inputfile, string filetemp1, string filetemp2, string filetemp3, string outputfile, string inputencoding, string inputhasheader)
    {
        logger.ZLogDebug("start arrangeandextract()");

        if (string.IsNullOrEmpty(inputfile) || string.IsNullOrEmpty(filetemp1)
         || string.IsNullOrEmpty(filetemp2) || string.IsNullOrEmpty(filetemp3)
         || string.IsNullOrEmpty(outputfile) || string.IsNullOrEmpty(inputencoding)
         || string.IsNullOrEmpty(inputhasheader))
        {
            logger.ZLogError($"Error: arg is NullOrEmpty.");
            return 1;
        }

        int iRet = 0;
        iRet = Arrange(inputfile, filetemp1, filetemp2, inputencoding, inputhasheader);
        if (iRet != 0)
        {
            logger.ZLogError($"Error: Arrange() is Error.");
            return iRet;
        }
        iRet = Extract(filetemp2, filetemp3, outputfile, "UTF-8", "true");
        if (iRet != 0)
        {
            logger.ZLogError($"Error: Extract() is Error.");
            return iRet;
        }

        logger.ZLogInformation("FILE:{0} is success.", inputfile);
        return 0;
    }

    [Command("arrange2andextract")]
    public int Arrange2AndExtract(string inputfile, string filetemp1, string filetemp2, string filetemp3, string filetemp4, string filetemp5, string outputfile, string inputencoding, string inputhasheader)
    {
        logger.ZLogDebug("start arrange2andextract()");

        if (string.IsNullOrEmpty(inputfile) || string.IsNullOrEmpty(filetemp1)
         || string.IsNullOrEmpty(filetemp2) || string.IsNullOrEmpty(filetemp3)
         || string.IsNullOrEmpty(filetemp4) || string.IsNullOrEmpty(filetemp5)
         || string.IsNullOrEmpty(outputfile) || string.IsNullOrEmpty(inputencoding)
         || string.IsNullOrEmpty(inputhasheader))
        {
            logger.ZLogError($"Error: arg is NullOrEmpty.");
            return 1;
        }

        int iRet = 0;
        iRet = Arrange(inputfile, filetemp1, filetemp2, inputencoding, inputhasheader, 1);
        if (iRet != 0)
        {
            logger.ZLogError($"Error: 1st Arrange() is Error.");
            return iRet;
        }
        iRet = Arrange(filetemp2, filetemp3, filetemp4, "UTF-8", "true", 2);
        if (iRet != 0)
        {
            logger.ZLogError($"Error: 2nd Arrange() is Error.");
            return iRet;
        }
        iRet = Extract(filetemp4, filetemp5, outputfile, "UTF-8", "true");
        if (iRet != 0)
        {
            logger.ZLogError($"Error: Extract() is Error.");
            return iRet;
        }

        logger.ZLogInformation("FILE:{0} is success.", inputfile);
        return 0;
    }

    [Command("arrange3andextract")]
    public int Arrange3AndExtract(string inputfile, string filetemp1, string filetemp2, string filetemp3, string filetemp4, string filetemp5, string filetemp6, string filetemp7, string outputfile, string inputencoding, string inputhasheader)
    {
        logger.ZLogDebug("start arrange3andextract()");

        if (string.IsNullOrEmpty(inputfile) || string.IsNullOrEmpty(filetemp1)
         || string.IsNullOrEmpty(filetemp2) || string.IsNullOrEmpty(filetemp3)
         || string.IsNullOrEmpty(filetemp4) || string.IsNullOrEmpty(filetemp5)
         || string.IsNullOrEmpty(filetemp6) || string.IsNullOrEmpty(filetemp7)
         || string.IsNullOrEmpty(outputfile) || string.IsNullOrEmpty(inputencoding)
         || string.IsNullOrEmpty(inputhasheader))
        {
            logger.ZLogError($"Error: arg is NullOrEmpty.");
            return 1;
        }

        int iRet = 0;
        iRet = Arrange(inputfile, filetemp1, filetemp2, inputencoding, inputhasheader, 1);
        if (iRet != 0)
        {
            logger.ZLogError($"Error: 1st Arrange() is Error.");
            return iRet;
        }
        iRet = Arrange(filetemp2, filetemp3, filetemp4, "UTF-8", "true", 2);
        if (iRet != 0)
        {
            logger.ZLogError($"Error: 2nd Arrange() is Error.");
            return iRet;
        }
        iRet = Arrange(filetemp4, filetemp5, filetemp6, "UTF-8", "true", 3);
        if (iRet != 0)
        {
            logger.ZLogError($"Error: 3rd Arrange() is Error.");
            return iRet;
        }
        iRet = Extract(filetemp6, filetemp7, outputfile, "UTF-8", "true");
        if (iRet != 0)
        {
            logger.ZLogError($"Error: Extract() is Error.");
            return iRet;
        }

        logger.ZLogInformation("FILE:{0} is success.", inputfile);
        return 0;
    }
}

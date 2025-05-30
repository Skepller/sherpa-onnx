﻿// Copyright (c)  2023  Xiaomi Corporation
// Copyright (c)  2023 by manyeyes
//
// This file shows how to use a streaming model to decode files
// Please refer to
// https://k2-fsa.github.io/sherpa/onnx/pretrained_models/online-transducer/zipformer-transducer-models.html
// to download streaming models

using CommandLine;
using CommandLine.Text;
using SherpaOnnx;

class OnlineDecodeFiles
{
  class Options
  {
    [Option(Required = true, HelpText = "Path to tokens.txt")]
    public string Tokens { get; set; } = string.Empty;

    [Option(Required = false, Default = "cpu", HelpText = "Provider, e.g., cpu, coreml")]
    public string Provider { get; set; } = string.Empty;

    [Option(Required = false, HelpText = "Path to transducer encoder.onnx")]
    public string Encoder { get; set; } = string.Empty;

    [Option(Required = false, HelpText = "Path to transducer decoder.onnx")]
    public string Decoder { get; set; } = string.Empty;

    [Option(Required = false, HelpText = "Path to transducer joiner.onnx")]
    public string Joiner { get; set; } = string.Empty;

    [Option("paraformer-encoder", Required = false, HelpText = "Path to paraformer encoder.onnx")]
    public string ParaformerEncoder { get; set; } = string.Empty;

    [Option("paraformer-decoder", Required = false, HelpText = "Path to paraformer decoder.onnx")]
    public string ParaformerDecoder { get; set; } = string.Empty;

    [Option("zipformer2-ctc", Required = false, HelpText = "Path to zipformer2 CTC onnx model")]
    public string Zipformer2Ctc { get; set; } = string.Empty;

    [Option("num-threads", Required = false, Default = 1, HelpText = "Number of threads for computation")]
    public int NumThreads { get; set; } = 1;

    [Option("decoding-method", Required = false, Default = "greedy_search",
            HelpText = "Valid decoding methods are: greedy_search, modified_beam_search")]
    public string DecodingMethod { get; set; } = "greedy_search";

    [Option(Required = false, Default = false, HelpText = "True to show model info during loading")]
    public bool Debug { get; set; } = false;

    [Option("sample-rate", Required = false, Default = 16000, HelpText = "Sample rate of the data used to train the model")]
    public int SampleRate { get; set; } = 16000;

    [Option("max-active-paths", Required = false, Default = 4,
        HelpText = @"Used only when --decoding--method is modified_beam_search.
It specifies number of active paths to keep during the search")]
    public int MaxActivePaths { get; set; } = 4;

    [Option("enable-endpoint", Required = false, Default = false,
        HelpText = "True to enable endpoint detection.")]
    public bool EnableEndpoint { get; set; } = false;

    [Option("rule1-min-trailing-silence", Required = false, Default = 2.4F,
        HelpText = @"An endpoint is detected if trailing silence in seconds is
larger than this value even if nothing has been decoded. Used only when --enable-endpoint is true.")]
    public float Rule1MinTrailingSilence { get; set; } = 2.4F;

    [Option("rule2-min-trailing-silence", Required = false, Default = 1.2F,
        HelpText = @"An endpoint is detected if trailing silence in seconds is
larger than this value after something that is not blank has been decoded. Used
only when --enable-endpoint is true.")]
    public float Rule2MinTrailingSilence { get; set; }  = 1.2F;

    [Option("rule3-min-utterance-length", Required = false, Default = 20.0F,
        HelpText = @"An endpoint is detected if the utterance in seconds is
larger than this value. Used only when --enable-endpoint is true.")]
    public float Rule3MinUtteranceLength { get; set; } = 20.0F;

    [Option("hotwords-file", Required = false, Default = "", HelpText = "Path to hotwords.txt")]
    public string HotwordsFile { get; set; } = string.Empty;

    [Option("hotwords-score", Required = false, Default = 1.5F, HelpText = "hotwords score")]
    public float HotwordsScore { get; set; } = 1.5F;

    [Option("rule-fsts", Required = false, Default = "",
            HelpText = "If not empty, path to rule fst for inverse text normalization")]
    public string RuleFsts { get; set; } = string.Empty;

    [Option("files", Required = true, HelpText = "Audio files for decoding")]
    public IEnumerable<string> Files { get; set; } = new string[] {};
  }

  static void Main(string[] args)
  {
    var parser = new CommandLine.Parser(with => with.HelpWriter = null);
    var parserResult = parser.ParseArguments<Options>(args);

    parserResult
      .WithParsed<Options>(options => Run(options))
      .WithNotParsed(errs => DisplayHelp(parserResult, errs));
  }

  private static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
  {
    string usage = @"
(1) Streaming transducer models

dotnet run \
  --tokens=./sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20/tokens.txt \
  --encoder=./sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20/encoder-epoch-99-avg-1.onnx \
  --decoder=./sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20/decoder-epoch-99-avg-1.onnx \
  --joiner=./sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20/joiner-epoch-99-avg-1.onnx \
  --num-threads=2 \
  --decoding-method=modified_beam_search \
  --debug=false \
  --files ./sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20/test_wavs/0.wav \
  ./sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20/test_wavs/1.wav

(2) Streaming Zipformer2 Ctc models

dotnet run -c Release \
  --tokens ./sherpa-onnx-streaming-zipformer-ctc-multi-zh-hans-2023-12-13/tokens.txt \
  --zipformer2-ctc ./sherpa-onnx-streaming-zipformer-ctc-multi-zh-hans-2023-12-13/ctc-epoch-20-avg-1-chunk-16-left-128.onnx \
  --files ./sherpa-onnx-streaming-zipformer-ctc-multi-zh-hans-2023-12-13/test_wavs/DEV_T0000000000.wav \
  ./sherpa-onnx-streaming-zipformer-ctc-multi-zh-hans-2023-12-13/test_wavs/DEV_T0000000001.wav \
  ./sherpa-onnx-streaming-zipformer-ctc-multi-zh-hans-2023-12-13/test_wavs/DEV_T0000000002.wav \
  ./sherpa-onnx-streaming-zipformer-ctc-multi-zh-hans-2023-12-13/test_wavs/TEST_MEETING_T0000000113.wav \
  ./sherpa-onnx-streaming-zipformer-ctc-multi-zh-hans-2023-12-13/test_wavs/TEST_MEETING_T0000000219.wav \
  ./sherpa-onnx-streaming-zipformer-ctc-multi-zh-hans-2023-12-13/test_wavs/TEST_MEETING_T0000000351.wav

(3) Streaming Paraformer models
dotnet run \
  --tokens=./sherpa-onnx-streaming-paraformer-bilingual-zh-en/tokens.txt \
  --paraformer-encoder=./sherpa-onnx-streaming-paraformer-bilingual-zh-en/encoder.int8.onnx \
  --paraformer-decoder=./sherpa-onnx-streaming-paraformer-bilingual-zh-en/decoder.int8.onnx \
  --num-threads=2 \
  --decoding-method=greedy_search \
  --debug=false \
  --files ./sherpa-onnx-streaming-paraformer-bilingual-zh-en/test_wavs/0.wav \
  ./sherpa-onnx-streaming-paraformer-bilingual-zh-en/test_wavs/1.wav

Please refer to
https://k2-fsa.github.io/sherpa/onnx/pretrained_models/online-transducer/index.html
https://k2-fsa.github.io/sherpa/onnx/pretrained_models/online-paraformer/index.html
https://k2-fsa.github.io/sherpa/onnx/pretrained_models/online-ctc/index.html
to download pre-trained streaming models.
";

    var helpText = HelpText.AutoBuild(result, h =>
    {
      h.AdditionalNewLineAfterOption = false;
      h.Heading = usage;
      h.Copyright = "Copyright (c) 2023 Xiaomi Corporation";
      return HelpText.DefaultParsingErrorsHandler(result, h);
    }, e => e);
    Console.WriteLine(helpText);
  }

  private static void Run(Options options)
  {
    var config = new OnlineRecognizerConfig();
    config.FeatConfig.SampleRate = options.SampleRate;

    // All models from icefall using feature dim 80.
    // You can change it if your model has a different feature dim.
    config.FeatConfig.FeatureDim = 80;

    config.ModelConfig.Transducer.Encoder = options.Encoder;
    config.ModelConfig.Transducer.Decoder = options.Decoder;
    config.ModelConfig.Transducer.Joiner = options.Joiner;

    config.ModelConfig.Paraformer.Encoder = options.ParaformerEncoder;
    config.ModelConfig.Paraformer.Decoder = options.ParaformerDecoder;

    config.ModelConfig.Zipformer2Ctc.Model = options.Zipformer2Ctc;

    config.ModelConfig.Tokens = options.Tokens;
    config.ModelConfig.Provider = options.Provider;
    config.ModelConfig.NumThreads = options.NumThreads;
    config.ModelConfig.Debug = options.Debug ? 1 : 0;

    config.DecodingMethod = options.DecodingMethod;
    config.MaxActivePaths = options.MaxActivePaths;
    config.EnableEndpoint = options.EnableEndpoint ? 1 : 0;

    config.Rule1MinTrailingSilence = options.Rule1MinTrailingSilence;
    config.Rule2MinTrailingSilence = options.Rule2MinTrailingSilence;
    config.Rule3MinUtteranceLength = options.Rule3MinUtteranceLength;
    config.HotwordsFile = options.HotwordsFile;
    config.HotwordsScore = options.HotwordsScore;
    config.RuleFsts = options.RuleFsts;

    var recognizer = new OnlineRecognizer(config);

    var files = options.Files.ToArray();

    // We create a separate stream for each file
    var streams = new List<OnlineStream>();
    streams.EnsureCapacity(files.Length);

    for (int i = 0; i != files.Length; ++i)
    {
      var s = recognizer.CreateStream();

      var waveReader = new WaveReader(files[i]);
      s.AcceptWaveform(waveReader.SampleRate, waveReader.Samples);

      var tailPadding = new float[(int)(waveReader.SampleRate * 0.3)];
      s.AcceptWaveform(waveReader.SampleRate, tailPadding);
      s.InputFinished();

      streams.Add(s);
    }

    while (true)
    {
      var readyStreams = streams.Where(s => recognizer.IsReady(s));
      if (!readyStreams.Any())
      {
        break;
      }

      recognizer.Decode(readyStreams);
    }

    // display results
    for (int i = 0; i != files.Length; ++i)
    {
      var r = recognizer.GetResult(streams[i]);
      var text = r.Text;
      var tokens = r.Tokens;
      Console.WriteLine("--------------------");
      Console.WriteLine(files[i]);
      Console.WriteLine("text: {0}", text);
      Console.WriteLine("tokens: [{0}]", string.Join(", ", tokens));
      Console.Write("timestamps: [");
      r.Timestamps.ToList().ForEach(i => Console.Write(string.Format("{0:0.00}", i) + ", "));
      Console.WriteLine("]");
    }
    Console.WriteLine("--------------------");
  }
}

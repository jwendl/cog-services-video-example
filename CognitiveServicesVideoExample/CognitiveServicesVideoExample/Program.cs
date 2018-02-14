using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Vision;
using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ClientException = Microsoft.ProjectOxford.Common.ClientException;
using EmotionClientException = Microsoft.ProjectOxford.Emotion.ClientException;
using VisionClientException = Microsoft.ProjectOxford.Vision.ClientException;

namespace CognitiveServicesVideoExample
{
    class Program
    {
        static string faceApiKey = "ba0f616beff3459fa7be18683b42684e";
        static string faceApiHost = "https://westus.api.cognitive.microsoft.com/face/v1.0";

        static string emotionApiKey = "3b9ac9e6d76747aebb28142cb73b5d8c";
        static string emotionApiHost = "https://westus.api.cognitive.microsoft.com/emotion/v1.0";

        static string visionApiKey = "6657d158036e40808cc6e826ec1abdf9";
        static string visionApiHost = "https://westus.api.cognitive.microsoft.com/vision/v1.0";

        static async Task Main(string[] args)
        {
            ServiceLocator.RegisterService<EmotionServiceClient, EmotionServiceClient>((sp) =>
            {
                return new EmotionServiceClient(emotionApiKey, emotionApiHost);
            });

            ServiceLocator.RegisterService<IFaceServiceClient, FaceServiceClient>((sp) =>
            {
                return new FaceServiceClient(faceApiKey, faceApiHost);
            });

            ServiceLocator.RegisterService<IVisionServiceClient, VisionServiceClient>((sp) =>
            {
                return new VisionServiceClient(visionApiKey, visionApiHost);
            });

            await ProcessVideoFile(new FileInfo(@"C:\Users\juswen\Downloads\Videos\room-movie.mov"));

            Console.WriteLine("Done processing video file...");
            Console.ReadKey();
        }

        static async Task ProcessVideoFile(FileInfo fileInfo)
        {
            // Opens MP4 file (ffmpeg is probably needed)
            var capture = new VideoCapture(fileInfo.FullName);
            int sleepTime = (int)Math.Round(1000 / capture.Fps);

            // Frame image buffer
            Mat matImage = new Mat();

            // When the movie playback reaches end, Mat.data becomes NULL.
            var frameCount = 0;
            while (true)
            {
                capture.Read(matImage); // same as cvQueryFrame
                if (matImage.Empty())
                {
                    break;
                }

                var analysisMemoryStream = matImage.ToMemoryStream(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 90));
                var faceMemoryStream = matImage.ToMemoryStream(".png", new ImageEncodingParam(ImwriteFlags.PngCompression, 3));
                var emotionStream = matImage.ToMemoryStream(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 100));
                if (analysisMemoryStream.Length > 20000)
                {
                    try
                    {
                        if (frameCount % 100 == 0)
                        {
                            await AnalyzeVisionAsync(analysisMemoryStream);
                            await AnalyzeFaceAsync(faceMemoryStream);
                            await AnalyzeEmotionAsync(emotionStream);
                            Console.WriteLine("-------------------");
                        }
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Exception: {exception.Message}");
                    }
                }

                frameCount++;
                Cv2.WaitKey(sleepTime);
            }
        }

        protected static async Task AnalyzeFaceAsync(Stream imageStream)
        {
            var faceServiceClient = ServiceLocator.GetRequiredService<IFaceServiceClient>();
            var faceAttributes = new List<FaceAttributeType>()
            {
                FaceAttributeType.Age,
                FaceAttributeType.Gender,
                FaceAttributeType.HeadPose
            };

            try
            {
                var faces = await faceServiceClient.DetectAsync(imageStream, returnFaceAttributes: faceAttributes);
                var facesJson = JsonConvert.SerializeObject(faces);
                Console.WriteLine($"Faces: {facesJson}");
            }
            catch (ClientException clientException)
            {
                Console.WriteLine($"Error: {clientException.Error.Message}");
            }
            catch (FaceAPIException faceApiException)
            {
                Console.WriteLine($"Error: {faceApiException.ErrorMessage}");
            }
        }

        protected static async Task AnalyzeVisionAsync(Stream imageStream)
        {
            var visualServiceClient = ServiceLocator.GetRequiredService<IVisionServiceClient>();
            var visualFeatures = new List<VisualFeature>()
            {
                VisualFeature.Adult,
                VisualFeature.Categories,
                VisualFeature.Color,
                VisualFeature.Description,
                VisualFeature.ImageType,
                VisualFeature.Tags,
            };

            try
            {
                var analysisResult = await visualServiceClient.AnalyzeImageAsync(imageStream, visualFeatures);
                var analysisJson = JsonConvert.SerializeObject(analysisResult);
                Console.WriteLine($"Analysis: {analysisJson}");
            }
            catch (VisionClientException clientException)
            {
                Console.WriteLine($"Error: {clientException.Error.Message}");
            }
        }

        protected static async Task AnalyzeEmotionAsync(Stream imageStream)
        {
            var emotionServiceClient = ServiceLocator.GetRequiredService<EmotionServiceClient>();
            try
            {
                var emotions = await emotionServiceClient.RecognizeAsync(imageStream);
                var emotionsJson = JsonConvert.SerializeObject(emotions);
                Console.WriteLine($"Emotions: {emotionsJson}");
            }
            catch (EmotionClientException clientException)
            {
                Console.WriteLine($"Error: {clientException.Error.Message}");
            }
        }
    }
}

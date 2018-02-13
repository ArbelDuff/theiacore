﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ObjectDetect.Models;
using TensorFlow;

namespace ObjectDetect
{
    public class GetTensorObject
    {
        private static IEnumerable<CatalogItem> _catalog;
        private static string _input = "input.jpg";
        private static string _catalogPath = "mscoco_label_map_nl.pbtxt";
        private static string _modelPath = "ssd_mobilenet_v1_coco_11_06_2017.pb";
        private static double MIN_SCORE_FOR_OBJECT_HIGHLIGHTING = 0.5;

        public GetTensorObject(string input)
        {
        }

        public static ImageHolder GetJsonFormat(string input)
        {
            _input = input;
            var list = new List<string>();
            _catalog = CatalogUtil.ReadCatalogItems(_catalogPath);
            string modelFile = _modelPath;

            using (var graph = new TFGraph())
            {
                var model = File.ReadAllBytes(modelFile);
                graph.Import(new TFBuffer(model));

                using (var session = new TFSession(graph))
                {
                    var tensor = ImageUtil.CreateTensorFromImageFile(_input, TFDataType.UInt8);

                    var runner = session.GetRunner();

                    runner
                        .AddInput(graph["image_tensor"][0], tensor)
                        .Fetch(
                            graph["detection_boxes"][0],
                            graph["detection_scores"][0],
                            graph["detection_classes"][0],
                            graph["num_detections"][0]);
                    var output = runner.Run();

                    var boxes = (float[,,])output[0].GetValue(jagged: false);
                    var scores = (float[,])output[1].GetValue(jagged: false);
                    var classes = (float[,])output[2].GetValue(jagged: false);
                    var num = (float[])output[3].GetValue(jagged: false);

                    return GetBoxes(boxes, scores, classes, _input, MIN_SCORE_FOR_OBJECT_HIGHLIGHTING);
                }
            }
        }

        private static ImageHolder GetBoxes(float[,,] boxes, float[,] scores, float[,] classes, string inputFile, double minScore)
        {
            //var boxesList = new List<ImageHolder>();

            var boxesList = new ImageHolder();

            var x = boxes.GetLength(0);
            var y = boxes.GetLength(1);
            var z = boxes.GetLength(2);
            float ymin = 0, xmin = 0, ymax = 0, xmax = 0;

            for (int i = 0; i < x; i++)
            {
                for (int j = 0; j < y; j++)
                {
                    if (scores[i, j] < minScore) continue;
                    int value = Convert.ToInt32(classes[i, j]);
                    for (int k = 0; k < z; k++)
                    {
                        var box = boxes[i, j, k];
                        switch (k)
                        {
                            case 0:
                                ymin = box;
                                break;
                            case 1:
                                xmin = box;
                                break;
                            case 2:
                                ymax = box;
                                break;
                            case 3:
                                xmax = box;
                                break;
                        }
                    }

                    CatalogItem catalogItem = _catalog.FirstOrDefault(item => item.Id == value);
                    if (!string.IsNullOrEmpty(catalogItem?.DisplayName))
                    {
                        Console.WriteLine(boxes);
                        var boxesItem = new ImageMetaData
                        {
                            Keyword = catalogItem.DisplayName,
                            Class = catalogItem.Id,
                            Left = xmin,
                            Right = xmax,
                            Bottom = ymin,
                            Top = ymax
                        };
                        boxesList.Data.Add(boxesItem);
                        boxesList.KeywordList.Add(catalogItem.DisplayName);
                    }
                }
            }
            return boxesList;
        }
    }
}
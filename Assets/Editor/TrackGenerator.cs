using SignRacers.Track;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// Builds a ready-made track from a list of points: Sign Racers > Generate Track (Hand).
    /// The road traces the outline of an open hand, seen from above: a thumb sticking out to the right,
    /// four fingers, a palm and a wrist. The outline was traced from a hand picture, then the valleys
    /// between the fingers were rounded so a 16-wide road fits, and the three valleys were lowered to
    /// the same height (600). It never crosses itself. The start/finish line is on the straight across the
    /// bottom of the wrist, and karts drive counterclockwise (up the thumb side first). The hand is
    /// about 630 wide and 1000 tall, and one lap is about 4,600 units (set Scale below 1 to shrink it; 0.9 is the smallest that still suits a 16-wide road).
    /// If the scene already has a Track, its shape is replaced (Cmd+Z undoes it); otherwise a new
    /// Track with a Road is created. The knots use Auto smoothing, so you can still drag any knot
    /// afterwards to adjust the shape by hand.
    /// </summary>
    public static class TrackGenerator
    {
        /// <summary>Make the whole track bigger or smaller. Below about 0.9 the finger gaps get too tight for a 16-wide road (0.85 needs a road of 12 or less).</summary>
        private const float Scale = 1f;

        /// <summary>Waypoints on the ground, x to the right and z up, in driving order. The first point is the start line.</summary>
        private static readonly Vector2[] Hand =
        {
            new Vector2(-0.4f, 0.0f), new Vector2(39.3f, 4.8f), new Vector2(78.0f, 14.5f), new Vector2(84.7f, 19.2f),
            new Vector2(88.8f, 26.2f), new Vector2(90.1f, 34.4f), new Vector2(89.9f, 42.6f), new Vector2(89.1f, 50.8f),
            new Vector2(87.4f, 65.5f), new Vector2(82.7f, 105.2f), new Vector2(78.0f, 144.9f), new Vector2(72.3f, 184.5f),
            new Vector2(71.6f, 196.9f), new Vector2(74.5f, 208.9f), new Vector2(82.0f, 218.7f), new Vector2(91.9f, 226.2f),
            new Vector2(104.2f, 234.6f), new Vector2(132.1f, 263.1f), new Vector2(151.7f, 297.9f), new Vector2(167.7f, 334.5f),
            new Vector2(183.3f, 371.4f), new Vector2(200.2f, 407.6f), new Vector2(219.1f, 442.9f), new Vector2(241.0f, 476.3f),
            new Vector2(266.4f, 507.2f), new Vector2(295.3f, 534.8f), new Vector2(304.9f, 544.1f), new Vector2(309.3f, 549.5f),
            new Vector2(312.8f, 555.6f), new Vector2(314.5f, 562.4f), new Vector2(313.3f, 569.3f), new Vector2(309.1f, 574.8f),
            new Vector2(303.1f, 578.3f), new Vector2(296.3f, 580.2f), new Vector2(289.3f, 580.9f), new Vector2(279.7f, 580.5f),
            new Vector2(263.9f, 577.0f), new Vector2(236.1f, 563.2f), new Vector2(205.4f, 537.6f), new Vector2(175.7f, 510.8f),
            new Vector2(168.1f, 505.1f), new Vector2(161.1f, 501.3f), new Vector2(153.3f, 499.3f), new Vector2(145.4f, 500.5f),
            new Vector2(139.0f, 505.1f), new Vector2(134.9f, 512.0f), new Vector2(132.7f, 519.7f), new Vector2(131.5f, 527.9f),
            new Vector2(130.7f, 539.8f), new Vector2(129.4f, 572.5f), new Vector2(129.3f, 612.5f), new Vector2(130.1f, 652.5f),
            new Vector2(131.5f, 692.4f), new Vector2(132.5f, 732.4f), new Vector2(134.3f, 772.4f), new Vector2(136.8f, 812.3f),
            new Vector2(137.6f, 852.3f), new Vector2(135.2f, 892.2f), new Vector2(127.6f, 918.5f), new Vector2(121.1f, 927.7f),
            new Vector2(111.9f, 934.2f), new Vector2(100.9f, 936.1f), new Vector2(90.1f, 933.2f), new Vector2(81.3f, 926.2f),
            new Vector2(75.1f, 915.4f), new Vector2(71.7f, 902.9f), new Vector2(69.0f, 885.1f), new Vector2(66.2f, 845.2f),
            new Vector2(65.5f, 805.2f), new Vector2(65.0f, 765.2f), new Vector2(64.5f, 725.2f), new Vector2(63.8f, 685.2f),
            new Vector2(61.5f, 645.3f), new Vector2(58.1f, 621.0f), new Vector2(56.2f, 614.3f), new Vector2(53.1f, 608.1f),
            new Vector2(48.2f, 603.1f), new Vector2(41.7f, 600.7f), new Vector2(34.9f, 601.7f), new Vector2(29.2f, 605.6f),
            new Vector2(25.2f, 611.4f), new Vector2(22.7f, 617.9f), new Vector2(21.1f, 625.0f), new Vector2(19.7f, 634.9f),
            new Vector2(18.0f, 651.1f), new Vector2(16.4f, 691.1f), new Vector2(15.7f, 731.1f), new Vector2(15.1f, 771.1f),
            new Vector2(14.5f, 811.1f), new Vector2(13.8f, 851.1f), new Vector2(11.4f, 891.0f), new Vector2(7.3f, 930.8f),
            new Vector2(0.0f, 970.1f), new Vector2(-4.1f, 981.0f), new Vector2(-9.5f, 989.7f), new Vector2(-16.9f, 996.2f),
            new Vector2(-26.4f, 998.9f), new Vector2(-36.1f, 996.9f), new Vector2(-44.4f, 991.5f), new Vector2(-50.6f, 983.7f),
            new Vector2(-55.6f, 972.0f), new Vector2(-58.9f, 956.1f), new Vector2(-61.1f, 933.5f), new Vector2(-61.9f, 893.5f),
            new Vector2(-60.4f, 853.5f), new Vector2(-57.3f, 813.7f), new Vector2(-53.9f, 773.8f), new Vector2(-50.6f, 733.9f),
            new Vector2(-47.6f, 694.1f), new Vector2(-45.3f, 654.1f), new Vector2(-45.6f, 618.0f), new Vector2(-47.5f, 611.3f),
            new Vector2(-51.3f, 605.4f), new Vector2(-57.0f, 601.5f), new Vector2(-63.9f, 600.7f), new Vector2(-70.3f, 603.3f),
            new Vector2(-75.3f, 608.2f), new Vector2(-78.7f, 614.3f), new Vector2(-81.0f, 620.9f), new Vector2(-83.2f, 629.3f),
            new Vector2(-86.0f, 643.4f), new Vector2(-93.5f, 682.7f), new Vector2(-100.4f, 722.0f), new Vector2(-107.4f, 761.4f),
            new Vector2(-114.4f, 800.8f), new Vector2(-122.0f, 840.1f), new Vector2(-131.2f, 879.0f), new Vector2(-146.0f, 916.0f),
            new Vector2(-154.6f, 925.9f), new Vector2(-164.3f, 931.5f), new Vector2(-175.4f, 932.6f), new Vector2(-185.9f, 928.6f),
            new Vector2(-194.0f, 920.8f), new Vector2(-199.7f, 910.3f), new Vector2(-203.5f, 895.8f), new Vector2(-204.6f, 875.2f),
            new Vector2(-201.2f, 840.3f), new Vector2(-195.1f, 800.8f), new Vector2(-188.2f, 761.4f), new Vector2(-180.6f, 722.1f),
            new Vector2(-173.0f, 682.8f), new Vector2(-165.5f, 643.6f), new Vector2(-163.3f, 630.2f), new Vector2(-162.8f, 622.7f),
            new Vector2(-163.3f, 615.7f), new Vector2(-165.5f, 609.1f), new Vector2(-169.8f, 603.7f), new Vector2(-176.1f, 600.8f),
            new Vector2(-183.0f, 601.3f), new Vector2(-189.1f, 604.6f), new Vector2(-193.9f, 609.7f), new Vector2(-197.6f, 615.6f),
            new Vector2(-200.9f, 622.7f), new Vector2(-204.9f, 633.5f), new Vector2(-213.1f, 659.8f), new Vector2(-221.8f, 698.9f),
            new Vector2(-230.2f, 738.0f), new Vector2(-239.2f, 776.9f), new Vector2(-253.3f, 814.3f), new Vector2(-261.7f, 826.1f),
            new Vector2(-271.9f, 833.9f), new Vector2(-283.8f, 837.3f), new Vector2(-295.2f, 835.9f), new Vector2(-305.1f, 830.0f),
            new Vector2(-311.5f, 820.4f), new Vector2(-314.4f, 809.3f), new Vector2(-314.9f, 796.9f), new Vector2(-313.3f, 781.0f),
            new Vector2(-306.3f, 745.9f), new Vector2(-298.2f, 706.7f), new Vector2(-288.2f, 668.0f), new Vector2(-277.4f, 629.5f),
            new Vector2(-266.4f, 591.1f), new Vector2(-255.7f, 552.5f), new Vector2(-245.9f, 513.7f), new Vector2(-237.7f, 474.6f),
            new Vector2(-230.5f, 435.2f), new Vector2(-224.0f, 395.8f), new Vector2(-217.4f, 356.3f), new Vector2(-208.6f, 317.3f),
            new Vector2(-196.0f, 279.4f), new Vector2(-177.6f, 243.9f), new Vector2(-154.0f, 211.6f), new Vector2(-148.4f, 195.3f),
            new Vector2(-148.2f, 177.9f), new Vector2(-150.0f, 156.8f), new Vector2(-152.8f, 116.9f), new Vector2(-154.8f, 76.9f),
            new Vector2(-154.9f, 37.0f), new Vector2(-151.6f, 27.7f), new Vector2(-144.7f, 20.8f), new Vector2(-136.0f, 16.1f),
            new Vector2(-126.9f, 12.4f), new Vector2(-111.3f, 7.6f), new Vector2(-71.9f, 1.0f), new Vector2(-32.0f, -1.1f),
        };

        [MenuItem("Sign Racers/Generate Track (Hand)")]
        public static void GenerateHandTrack() => Generate("hand", Hand, Scale);

        [MenuItem("Sign Racers/Generate Track (Circle)")]
        public static void GenerateCircleTrack()
        {
            // A plain round track: 32 points on a circle of radius 150 (a lap of about 940 units).
            // The start line is at the bottom of the circle and karts drive counterclockwise.
            const int count = 32;
            const float radius = 150f;
            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float angle = (-90f + 360f * i / count) * Mathf.Deg2Rad;
                points[i] = new Vector2(radius * Mathf.Cos(angle), radius + radius * Mathf.Sin(angle));
            }
            Generate("circle", points, 1f);
        }

        private static void Generate(string shapeName, Vector2[] points, float scale)
        {
            var existing = Object.FindFirstObjectByType<TrackPath>();
            GameObject trackObject;

            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Replace the track shape?",
                        "This replaces the shape of '" + existing.name + "' with the " + shapeName + " track. " +
                        "Its Road, start gate and placed objects stay. You can undo with Cmd+Z. Continue?",
                        "Replace", "Cancel"))
                    return;
                trackObject = existing.gameObject;
            }
            else
            {
                trackObject = new GameObject("Track", typeof(SplineContainer), typeof(TrackPath));
                Undo.RegisterCreatedObjectUndo(trackObject, "Generate Track");

                var road = new GameObject("Road", typeof(TrackRoadGenerator));
                road.transform.SetParent(trackObject.transform, false);
            }

            var container = trackObject.GetComponent<SplineContainer>();
            Undo.RecordObject(container, "Generate Track");
            if (container.Splines.Count == 0) container.AddSpline(new Spline());

            Spline spline = container.Spline;
            spline.Clear();
            foreach (Vector2 p in points)
                spline.Add(new BezierKnot(new float3(p.x * scale, 0f, p.y * scale)), TangentMode.AutoSmooth);
            spline.Closed = true;
            EditorUtility.SetDirty(container);

            var path = trackObject.GetComponent<TrackPath>();
            path.Rebuild();
            foreach (var road in trackObject.GetComponentsInChildren<TrackRoadGenerator>()) road.Generate();

            EditorSceneManager.MarkSceneDirty(trackObject.scene);
            Selection.activeGameObject = trackObject;

            EditorUtility.DisplayDialog("Track generated",
                $"One lap is about {path.TotalLength:F0} units long. At a kart speed of 8 that is roughly {path.TotalLength / 8f:F0} seconds " +
                "(boosts make it faster). Check the shape in the Scene view and save the scene.", "OK");
        }
    }
}

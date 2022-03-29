using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertCurve
{
    class Program
    {
        /// <summary>
        /// Stores pre-computed Sierpinski curves, where each curve contains an array of the x-coordinates and an array of the y-coordinates
        /// </summary>
        public static List<Tuple<int[], int[]>> PreComputedSierpinskiCurves;

        /// <summary>
        /// Stores pre-computed pyramids, where each pyramid contains list(s) of the x-coorindates and y-coordinates, indexed by layer
        /// </summary>
        public static List<Tuple<List<double[]>, List<double[]>>> PreComputedSierpinskiPyramids;

        /// <summary>
        /// For each pre-computed pyramid, for each layer, stores the index offset needed to start the curve at the northeast ridge, entering through the north face.
        /// </summary>
        public static List<List<int>> PreComputedSierpinskiPyramidLayerIndexOffsets;

        // Used for doing the 90 degree rotations of the Sierpinski Curve
        static int[] cosines = new int[] { 1, 0, -1, 0 };
        static int[] sines = new int[] { 0, 1, 0, -1 };

        // Length in mm of one "step" of the Sierpinski Curve grid: each of the smallest pyramids has a base that is a 2*gridStep by 2*gridStep square.
        const double gridStep = 0.85;
        const double sqrt2 = 1.4142135623730952; // square root of 2
        const double gridHeight = gridStep * sqrt2;

        // Layer height in mm
        const int layersPerPyramid = 6; // number of layers for the smallest pyramids
        const double layerHeight = gridHeight / layersPerPyramid; // approximately 0.2 mm -- we want each subpyramid to be an exact integer multiple of layers high

        static void Main(string[] args)
        {
            const string outputFolder = @"C:\Users\info\source\repos\SierpinskiPyramid";

            // Whether to include the filament change after the first layer
            const bool includeFilamentChange = true;

            // Number of extra steps to take in the first layer if inluding a filament change
            const int firstLayerExtraSteps = 3;

            // Starting position of the main Sierpinski Curve
            const double xCenter = 125.0;
            const double yCenter = 105.0;

            const double stepsPerSecond = 58.69; // "Steps" here means steps of the Sierpinski Curve. Note: if gridStep changes, this will also change
            const double oneMinuteInSteps = 60 * stepsPerSecond; // For convenience, stores the equivalent number of 'steps' for one minute's worth of time

            // Minutes to home and calibrate
            const double homeAndCalibrateMinutes = 0.5;

            // Minutes to draw the intro line
            const double introLineMinutes = 0.12;

            // Border padding in mm
            const double borderPadding = 5.0;

            // extrusion rate per mm (not sure what the units on the extruder motor are)
            const double extrusionRate = 0.032715;

            // Feed rate for printing (not sure but I think the units on this are millimeters per minute)
            const double printFeedRate = 1200.000;

            PreComputedSierpinskiCurves = new List<Tuple<int[], int[]>>();
            PreComputedSierpinskiPyramids = new List<Tuple<List<double[]>, List<double[]>>>();
            PreComputedSierpinskiPyramidLayerIndexOffsets = new List<List<int>>();

            const int exponent = 7; // Power of 2 for which we are generating a sierpinski pyramid
            GenerateSierpinskiPyramid(exponent, out List<double[]> xCoords, out List<double[]> yCoords, out List<int> layerIndexOffsets, out int sideLen);

            #region Matlab Visualization

            string outputMFilename = "SierpinskiCurve_plot.m";
            string outputMPath = Path.Combine(outputFolder, outputMFilename);
            using (StreamWriter sw = new StreamWriter(outputMPath, false))
            {
                int[] xCoordsLastCurve = PreComputedSierpinskiCurves[exponent].Item1;
                int[] yCoordsLastCurve = PreComputedSierpinskiCurves[exponent].Item2;
                int nPtsLastCurve = xCoordsLastCurve.Length;
                sw.WriteLine("close all");
                sw.WriteLine("clear all");
                sw.WriteLine("clc");
                sw.Write("xCoords = [");
                for (int i = 0; i < nPtsLastCurve; i++)
                {
                    sw.Write(xCoordsLastCurve[i].ToString() + " ");
                }
                sw.WriteLine("];");
                sw.Write("yCoords = [");
                for (int i = 0; i < nPtsLastCurve; i++)
                {
                    sw.Write(yCoordsLastCurve[i].ToString() + " ");
                }
                sw.WriteLine("];");
                sw.WriteLine("plot(xCoords, yCoords, 'r-');");
                sw.WriteLine("axis equal;");
            }

            outputMFilename = "SierpinskiPyramid_plot.m";
            outputMPath = Path.Combine(outputFolder, outputMFilename);
            using (StreamWriter sw = new StreamWriter(outputMPath, false))
            {
                int nLayersLastPyramid = PreComputedSierpinskiPyramids[exponent].Item1.Count;

                sw.WriteLine("close all");
                sw.WriteLine("clear all");
                sw.WriteLine("clc");
                sw.Write("xCoords = [");
                for (int layerNumber = 0; layerNumber < nLayersLastPyramid; layerNumber++)
                {
                    double[] xCoordsCurve = PreComputedSierpinskiPyramids[exponent].Item1[layerNumber];
                    int nPtsCurve = xCoordsCurve.Length;

                    for (int i = 0; i < nPtsCurve; i++)
                    {
                        sw.Write(xCoordsCurve[i].ToString() + " ");
                    }
                }
                sw.WriteLine("];");
                sw.Write("yCoords = [");
                for (int layerNumber = 0; layerNumber < nLayersLastPyramid; layerNumber++)
                {
                    double[] yCoordsCurve = PreComputedSierpinskiPyramids[exponent].Item2[layerNumber];
                    int nPtsCurve = yCoordsCurve.Length;

                    for (int i = 0; i < nPtsCurve; i++)
                    {
                        sw.Write(yCoordsCurve[i].ToString() + " ");
                    }
                }
                sw.WriteLine("];");
                sw.Write("zCoords = [");
                for (int layerNumber = 0; layerNumber < nLayersLastPyramid; layerNumber++)
                {
                    int nPtsCurve = PreComputedSierpinskiPyramids[exponent].Item1[layerNumber].Length;

                    for (int i = 0; i < nPtsCurve; i++)
                    {
                        sw.Write((layerNumber * layerHeight).ToString() + " ");
                    }
                }
                sw.WriteLine("];");
                sw.WriteLine("plot3(xCoords, yCoords, zCoords, 'r-');");
                sw.WriteLine("axis equal;");
            }

            outputMFilename = "SierpinskiPyramidOneLayer_plot.m";
            const int layerNumberToPlot = 12;
            outputMPath = Path.Combine(outputFolder, outputMFilename);
            using (StreamWriter sw = new StreamWriter(outputMPath, false))
            {
                int nLayersLastPyramid = PreComputedSierpinskiPyramids[exponent].Item1.Count;

                sw.WriteLine("close all");
                sw.WriteLine("clear all");
                sw.WriteLine("clc");
                sw.Write("xCoords = [");

                double[] xCoordsCurve = PreComputedSierpinskiPyramids[exponent].Item1[layerNumberToPlot];
                int nPtsCurve = xCoordsCurve.Length;

                for (int i = 0; i < nPtsCurve; i++)
                {
                    sw.Write(xCoordsCurve[i].ToString() + " ");
                }

                sw.WriteLine("];");
                sw.Write("yCoords = [");

                double[] yCoordsCurve = PreComputedSierpinskiPyramids[exponent].Item2[layerNumberToPlot];

                for (int i = 0; i < nPtsCurve; i++)
                {
                    sw.Write(yCoordsCurve[i].ToString() + " ");
                }

                sw.WriteLine("];");
                sw.WriteLine("plot(xCoords, yCoords, 'r-');");
                sw.WriteLine("hold on");
                sw.WriteLine("plot(" + xCoordsCurve[0] + ", " + yCoordsCurve[0] + ", 'bo');");
                int idx = PreComputedSierpinskiPyramidLayerIndexOffsets[exponent][layerNumberToPlot];
                sw.WriteLine("plot(" + xCoordsCurve[idx] + ", " + yCoordsCurve[idx] + ", 'go');");
                sw.WriteLine("title('Layer " + layerNumberToPlot.ToString() + "')");
                sw.WriteLine("axis equal;");
            }

            #endregion Matlab Visualization

            string layerHeightText = string.Format("{0,1:F1}mm", layerHeight); // as it will appear in the filename

            // Total number of Sierpinski curve steps taken
            int nLayers = xCoords.Count;
            int totalSierpinskiSteps = 0;
            for (int i = 0; i < nLayers; i++)
            {
                totalSierpinskiSteps += xCoords[i].Length + 1;
            }

            // Print time for just the print portions that are Sierpinski-Curves
            double sierpinskiPrintTimeInMinutes = totalSierpinskiSteps / oneMinuteInSteps;
            Console.WriteLine("Grid size (mm): " + (sideLen * gridStep).ToString());

            #region Determine total print time

            double preDistanceFirstLayer = 4 * (sideLen * gridStep + 2 * borderPadding);

            double totalMinutes = sierpinskiPrintTimeInMinutes // Print time for the Hilbert Curve
                + (preDistanceFirstLayer / printFeedRate) // Add time to draw border pattern
                + introLineMinutes // add time for drawing of the intro line
                + (includeFilamentChange ? introLineMinutes : 0) // add time for drawing of the second intro line
                + homeAndCalibrateMinutes; // add time for homing and calibrating

            int printMinutes = (int)Math.Round(totalMinutes);
            int printHours = 0;
            while (printMinutes >= 60)
            {
                printHours++;
                printMinutes -= 60;
            }

            // Print the time until the filament change
            double minutesUntilFilamentChange = ((xCoords[0].Length + 1) / oneMinuteInSteps) // Print time for the first layer
                + (preDistanceFirstLayer / printFeedRate) // Add time to draw border pattern
                + introLineMinutes // add time for drawing of the intro line
                + homeAndCalibrateMinutes; // add time for homing and calibrating
            Console.WriteLine("Minutes until filament change: " + minutesUntilFilamentChange.ToString());

            #endregion Determine total print time

            string outputFilename = "SierpinskiPyramid_" + layerHeightText + "_PLA_MK3S_" + printHours.ToString() + "h" + printMinutes + "m.gcode";

            string outputPath = Path.Combine(outputFolder, outputFilename);
            using (StreamWriter sw = new StreamWriter(outputPath))
            {
                #region Initial G-Code

                sw.WriteLine("; Custom gcode file generated from C# code");
                sw.WriteLine("; filament extrusion speed = " + extrusionRate.ToString());
                sw.WriteLine("");
                sw.WriteLine("M201 X9000 Y9000 Z500 E10000 ; sets maximum accelerations, mm/sec^2");
                sw.WriteLine("M203 X500 Y500 Z12 E120 ; sets maximum feedrates, mm/sec");
                sw.WriteLine("M204 P1500 R1500 T1500 ; sets acceleration (P, T) and retract acceleration (R), mm/sec^2");
                sw.WriteLine("M205 X10.00 Y10.00 Z0.20 E2.50 ; sets the jerk limits, mm/sec");
                sw.WriteLine("M205 S0 T0 ; sets the minimum extruding and travel feed rate, mm/sec");
                sw.WriteLine("M107");
                sw.WriteLine("M115 U3.3.1 ; tell printer latest fw version");
                sw.WriteLine("M201 X1000 Y1000 Z1000 E9000 ; sets maximum accelerations, mm/sec^2");
                sw.WriteLine("M203 X200 Y200 Z12 E120 ; sets maximum feedrates, mm/sec");
                sw.WriteLine("M204 S1250 T1250 ; sets acceleration (S) and retract acceleration (T)");
                sw.WriteLine("M205 X8 Y8 Z0.4 E1.5 ; sets the jerk limits, mm/sec");
                sw.WriteLine("M205 S0 T0 ; sets the minimum extruding and travel feed rate, mm/sec");
                sw.WriteLine("M83  ; extruder relative mode");
                sw.WriteLine("M104 S215 ; set extruder temp");
                sw.WriteLine("M140 S60 ; set bed temp");
                sw.WriteLine("M190 S60 ; wait for bed temp");
                sw.WriteLine("M109 S215 ; wait for extruder temp");
                sw.WriteLine("M73 Q0 S" + ((int)Math.Round(totalMinutes)).ToString() + " ; updating progress display (0% done, " + ((int)Math.Round(totalMinutes)).ToString() + " minutes remaining)");
                sw.WriteLine("M73 P0 R" + ((int)Math.Round(totalMinutes)).ToString() + " ; updating progress display (0% done, " + ((int)Math.Round(totalMinutes)).ToString() + " minutes remaining)");
                sw.WriteLine("G28 W ; home all without mesh bed level");
                sw.WriteLine("G80 ; mesh bed leveling");
                double minutesElapsed = homeAndCalibrateMinutes;
                int minutesRemaining = (int)Math.Round(totalMinutes - minutesElapsed);
                int pctDone = (int)Math.Round(100 * minutesElapsed / totalMinutes);
                sw.WriteLine("M73 Q" + pctDone.ToString() + " S" + minutesRemaining.ToString() + " ; updating progress display (" + pctDone.ToString() + "% done, " + minutesRemaining.ToString() + " minutes remaining)");
                sw.WriteLine("M73 P" + pctDone.ToString() + " R" + minutesRemaining.ToString() + " ; updating progress display (" + pctDone.ToString() + "% done, " + minutesRemaining.ToString() + " minutes remaining)");
                sw.WriteLine("G1 Y-3.0 F1000.0 ; go outside print area");
                sw.WriteLine("G92 E0.0");
                sw.WriteLine("G1 X60.0 E9.0  F1000.0 ; intro line");
                sw.WriteLine("M73 Q0 S" + ((int)Math.Round(totalMinutes)).ToString() + " ; updating progress display (0% done, " + ((int)Math.Round(totalMinutes)).ToString() + " minutes remaining)");
                sw.WriteLine("M73 P0 R" + ((int)Math.Round(totalMinutes)).ToString() + " ; updating progress display (0% done, " + ((int)Math.Round(totalMinutes)).ToString() + " minutes remaining)");
                sw.WriteLine("G1 X100.0 E12.5  F1000.0 ; intro line");
                minutesElapsed = homeAndCalibrateMinutes + introLineMinutes;
                minutesRemaining = (int)Math.Round(totalMinutes - minutesElapsed);
                pctDone = (int)Math.Round(100 * minutesElapsed / totalMinutes);
                sw.WriteLine("M73 Q" + pctDone.ToString() + " S" + minutesRemaining.ToString() + " ; updating progress display (" + pctDone.ToString() + "% done, " + minutesRemaining.ToString() + " minutes remaining)");
                sw.WriteLine("M73 P" + pctDone.ToString() + " R" + minutesRemaining.ToString() + " ; updating progress display (" + pctDone.ToString() + "% done, " + minutesRemaining.ToString() + " minutes remaining)");
                sw.WriteLine("G92 E0.0");
                sw.WriteLine("M221 S95");
                sw.WriteLine("M900 K30; Filament gcode");
                sw.WriteLine("G21 ; set units to millimeters");
                sw.WriteLine("G90 ; use absolute coordinates");
                sw.WriteLine("M83 ; use relative distances for extrusion");
                sw.WriteLine(";BEFORE_LAYER_CHANGE");
                sw.WriteLine("G92 E0.0");
                sw.WriteLine(";0.2");
                sw.WriteLine("");
                sw.WriteLine("");
                sw.WriteLine("G1 E-0.80000 F2100.00000 ; retract filament");
                sw.WriteLine("G1 Z0.600 F10800.000 ; lift tip up");
                sw.WriteLine(";AFTER_LAYER_CHANGE");
                sw.WriteLine(";0.2");
                double xMM = Math.Round(xCenter, 3); // Bottom center of the border
                double yMM = Math.Round(yCenter - 0.5 * sideLen * gridStep - borderPadding, 3); // Bottom center of the border
                sw.WriteLine("G1 X" + string.Format("{0,1:F3}", xMM) + " Y" + string.Format("{0,1:F3}", yMM) + " ; go to extrusion start position");
                double xMMPrev = xMM;
                double yMMPrev = yMM;
                sw.WriteLine("G1 Z" + string.Format("{0,1:F3}", Math.Round(layerHeight, 3)) + " ; bring tip back down to first layer");
                sw.WriteLine("G1 E0.80000 F2100.00000 ; ready filament");
                sw.WriteLine("M204 S1000");
                sw.WriteLine("G1 F" + string.Format("{0,1:F3}", printFeedRate) + " ; restore feed rate to that used for printing (not sure but I think the units on this are millimeters per minute)");
                sw.WriteLine("");

                sw.WriteLine("; Draw border");
                xMM = Math.Round(xCenter + 0.5 * sideLen * gridStep + borderPadding, 3); // Move x to the right of the border
                double distanceTraveled = Math.Sqrt(Math.Pow(xMM - xMMPrev, 2) + Math.Pow(yMM - yMMPrev, 2));
                sw.WriteLine("G1 X" + string.Format("{0,1:F3}", xMM) + " Y" + string.Format("{0,1:F3}", yMM) + " E" + string.Format("{0,1:F5}", Math.Round(extrusionRate * distanceTraveled, 5)));
                xMMPrev = xMM;
                yMMPrev = yMM;

                yMM = Math.Round(yCenter + 0.5 * sideLen * gridStep + borderPadding, 3); // Move y to the top of the border
                distanceTraveled = Math.Sqrt(Math.Pow(xMM - xMMPrev, 2) + Math.Pow(yMM - yMMPrev, 2));
                sw.WriteLine("G1 X" + string.Format("{0,1:F3}", xMM) + " Y" + string.Format("{0,1:F3}", yMM) + " E" + string.Format("{0,1:F5}", Math.Round(extrusionRate * distanceTraveled, 5)));
                xMMPrev = xMM;
                yMMPrev = yMM;

                xMM = Math.Round(xCenter - 0.5 * sideLen * gridStep - borderPadding, 3); // Move x to the left of the border
                distanceTraveled = Math.Sqrt(Math.Pow(xMM - xMMPrev, 2) + Math.Pow(yMM - yMMPrev, 2));
                sw.WriteLine("G1 X" + string.Format("{0,1:F3}", xMM) + " Y" + string.Format("{0,1:F3}", yMM) + " E" + string.Format("{0,1:F5}", Math.Round(extrusionRate * distanceTraveled, 5)));
                xMMPrev = xMM;
                yMMPrev = yMM;

                yMM = Math.Round(yCenter - 0.5 * sideLen * gridStep - borderPadding, 3); // Move y to the bottom of the border
                distanceTraveled = Math.Sqrt(Math.Pow(xMM - xMMPrev, 2) + Math.Pow(yMM - yMMPrev, 2));
                sw.WriteLine("G1 X" + string.Format("{0,1:F3}", xMM) + " Y" + string.Format("{0,1:F3}", yMM) + " E" + string.Format("{0,1:F5}", Math.Round(extrusionRate * distanceTraveled, 5)));
                xMMPrev = xMM;
                yMMPrev = yMM;

                xMM = Math.Round(xCenter, 3); // Move x to the center
                distanceTraveled = Math.Sqrt(Math.Pow(xMM - xMMPrev, 2) + Math.Pow(yMM - yMMPrev, 2));
                sw.WriteLine("G1 X" + string.Format("{0,1:F3}", xMM) + " Y" + string.Format("{0,1:F3}", yMM) + " E" + string.Format("{0,1:F5}", Math.Round(extrusionRate * distanceTraveled, 5)));
                xMMPrev = xMM;
                yMMPrev = yMM;

                sw.WriteLine("G1 E-0.80000 F2100.00000 ; retract filament");
                sw.WriteLine("G1 Z0.600 F10800.000 ; lift tip up");

                // Move to the first point of the pattern
                xMM = Math.Round(xCenter + xCoords[0][0], 3);
                yMM = Math.Round(yCenter + yCoords[0][0], 3);
                distanceTraveled = Math.Sqrt(Math.Pow(xMM - xMMPrev, 2) + Math.Pow(yMM - yMMPrev, 2));
                sw.WriteLine("G1 X" + string.Format("{0,1:F3}", xMM) + " Y" + string.Format("{0,1:F3}", yMM));

                sw.WriteLine("G1 Z" + string.Format("{0,1:F3}", Math.Round(layerHeight, 3)) + " ; bring tip back down to first layer");
                sw.WriteLine("G1 E0.80000 F2100.00000 ; ready filament");
                sw.WriteLine("M204 S1000");
                sw.WriteLine("G1 F" + string.Format("{0,1:F3}", printFeedRate) + " ; restore feed rate to that used for printing (not sure but I think the units on this are millimeters per minute)");

                // Update progress display
                minutesElapsed = homeAndCalibrateMinutes + introLineMinutes + (preDistanceFirstLayer / printFeedRate);
                minutesRemaining = (int)Math.Round(totalMinutes - minutesElapsed);
                pctDone = (int)Math.Round(100 * minutesElapsed / totalMinutes);
                sw.WriteLine("M73 Q" + pctDone.ToString() + " S" + minutesRemaining.ToString() + " ; updating progress display (" + pctDone.ToString() + "% done, " + minutesRemaining.ToString() + " minutes remaining)");
                sw.WriteLine("M73 P" + pctDone.ToString() + " R" + minutesRemaining.ToString() + " ; updating progress display (" + pctDone.ToString() + "% done, " + minutesRemaining.ToString() + " minutes remaining)");
                sw.WriteLine("; PURGING FINISHED");
                sw.WriteLine("");
                sw.WriteLine("; note: approximate bed center = (125, 105)");
                sw.WriteLine("");
                sw.WriteLine("; Sierpinski Pyramid Pattern");

                #endregion Initial G-Code

                int step = 0;
                minutesRemaining = (int)Math.Round(sierpinskiPrintTimeInMinutes);
                int percentDone = 0;
                double prevX = xMM;
                double prevY = yMM;
                for (int layerNumber = 0; layerNumber < nLayers; layerNumber++)
                {
                    if (layerNumber > 0)
                    {
                        sw.WriteLine("M106 S255 ; turn on the fan"); // turn on the fan
                    }

                    double[] xCoordsCurve = xCoords[layerNumber];
                    double[] yCoordsCurve = yCoords[layerNumber];
                    int nPtsCurve = xCoordsCurve.Length;
                    for (int vIdx = 0; vIdx < nPtsCurve; vIdx++)
                    {
                        if ((layerNumber == 0 && vIdx == 0) // Skip moving to the first vertex, since we're already there
                            || (includeFilamentChange && layerNumber == 1 && vIdx == 0)) // First vertex of second layer will also be skipped if we're including the filament change
                        {
                            continue;
                        }

                        // Are we at the first vertex of a new layer?
                        if (layerNumber > 0 && vIdx == 0)
                        {
                            // Extrude directly to the next layer (forming one continuous curve), but only for layers after the first laye
                                sw.WriteLine("G1 Z" + string.Format("{0,1:F3}", Math.Round((layerNumber + 1) * layerHeight, 3)) +
                                    " E" + string.Format("{0,1:F5}", Math.Round(extrusionRate * layerHeight, 5)));
                        }

                        distanceTraveled = Math.Sqrt(Math.Pow(xCenter + xCoordsCurve[vIdx] - prevX, 2) + Math.Pow(yCenter + yCoordsCurve[vIdx] - prevY, 2));

                        // Extrude 1 step to the next vertex
                        sw.WriteLine("G1 X" +
                            string.Format("{0,1:F3}", Math.Round(xCenter + xCoordsCurve[vIdx], 3)) +
                            " Y" +
                            string.Format("{0,1:F3}", Math.Round(yCenter + yCoordsCurve[vIdx], 3)) +
                            " E" +
                            string.Format("{0,1:F5}", Math.Round(extrusionRate * distanceTraveled, 5)));

                        // Add to the number of steps taken (used for updating the progress display)
                        step++;

                        // Update progress display, if needed
                        minutesElapsed = homeAndCalibrateMinutes + introLineMinutes + (preDistanceFirstLayer / printFeedRate) + (step / oneMinuteInSteps)
                            + (includeFilamentChange && layerNumber > 0 ? introLineMinutes : 0);
                        int nextMinutesRemaining = (int)Math.Round(totalMinutes - minutesElapsed);
                        int nextPercentDone = (int)Math.Round(100 * minutesElapsed / totalMinutes);
                        if (nextMinutesRemaining != minutesRemaining || nextPercentDone != percentDone)
                        {
                            minutesRemaining = nextMinutesRemaining;
                            percentDone = nextPercentDone;
                            sw.WriteLine("M73 Q" + percentDone.ToString() + " S" + minutesRemaining.ToString());
                            sw.WriteLine("M73 P" + percentDone.ToString() + " R" + minutesRemaining.ToString());
                        }

                        // Record the previous position
                        prevX = xCenter + xCoordsCurve[vIdx];
                        prevY = yCenter + yCoordsCurve[vIdx];
                    }

                    if (includeFilamentChange && layerNumber == 0)
                    {
                        for (int i = 0; i < firstLayerExtraSteps; i++)
                        {
                            distanceTraveled = Math.Sqrt(Math.Pow(xCenter + xCoordsCurve[i] - prevX, 2) + Math.Pow(yCenter + yCoordsCurve[i] - prevY, 2));

                            // Extrude 1 step to the starting vertex of layer 0
                            sw.WriteLine("G1 X" +
                                string.Format("{0,1:F3}", Math.Round(xCenter + xCoordsCurve[i], 3)) +
                                " Y" +
                                string.Format("{0,1:F3}", Math.Round(yCenter + yCoordsCurve[i], 3)) +
                                " E" +
                                string.Format("{0,1:F5}", Math.Round(extrusionRate * distanceTraveled, 5)));
                            // Record the previous position
                            prevX = xCenter + xCoordsCurve[i];
                            prevY = yCenter + yCoordsCurve[i];
                        }

                        // Raise Z to second layer
                        sw.WriteLine("G1 Z" + string.Format("{0,1:F3}", Math.Round(2 * layerHeight, 3)));

                        // Do wipe move thingy while retracting filament -0.8 units total
                        sw.WriteLine("; retracting extruder");
                        sw.WriteLine("G1 F8640;_WIPE");
                        sw.WriteLine("G1 X" + (prevX - 0.603).ToString() + " Y" + (prevY - 0.603).ToString() + " E-0.19691");
                        sw.WriteLine("G1 F8640;_WIPE");
                        sw.WriteLine("G1 X" + (prevX - 1.143).ToString() + " Y" + (prevY - 0.603).ToString() + " E-0.12474");
                        sw.WriteLine("G1 F8640;_WIPE");
                        sw.WriteLine("G1 X" + (prevX - 0.17).ToString() + " Y" + (prevY + 0.37).ToString() + " E-0.31790");
                        sw.WriteLine("G1 F8640;_WIPE");
                        sw.WriteLine("G1 X" + (prevX - 0.17).ToString() + " Y" + (prevY + 0.892).ToString() + " E-0.12046");
                        sw.WriteLine("G1 E-0.04000 F2100.00000");

                        // Raise Z to 10mm
                        sw.WriteLine("G1 Z" + string.Format("{0,1:F3}", Math.Round(10.0, 3)));

                        // Update progress display
                        minutesElapsed = homeAndCalibrateMinutes + introLineMinutes + (preDistanceFirstLayer / printFeedRate) + (step / oneMinuteInSteps)
                            + (includeFilamentChange && layerNumber > 0 ? introLineMinutes : 0);
                        minutesRemaining = (int)Math.Round(totalMinutes - minutesElapsed);
                        percentDone = (int)Math.Round(100 * minutesElapsed / totalMinutes);
                        sw.WriteLine("M73 Q" + percentDone.ToString() + " S" + minutesRemaining.ToString());
                        sw.WriteLine("M73 P" + percentDone.ToString() + " R" + minutesRemaining.ToString());

                        // Change filament
                        sw.WriteLine("M600");

                        // Repeat the intro line, but offset 3 mm from the first one
                        sw.WriteLine("G28 W ; home all without mesh bed level");
                        sw.WriteLine("G1 Y0.0 F1000.0 ; go outside print area");
                        sw.WriteLine("G1 E0.80000 F2100.00000; ready filament");
                        sw.WriteLine("G92 E0.0");
                        sw.WriteLine("G1 X60.0 E9.0  F1000.0 ; intro line");
                        sw.WriteLine("G1 X100.0 E12.5  F1000.0 ; intro line");
                        sw.WriteLine("G92 E0.0");
                        sw.WriteLine("G1 E-0.80000 F2100.00000 ; retract filament");
                        sw.WriteLine("G1 Z0.800 F10800.000 ; lift tip up");

                        // Go to layer 2 starting position and ready filament
                        sw.WriteLine("G1"
                            + " X" + string.Format("{0,1:F3}", Math.Round(xCenter + xCoords[1][0], 3))
                            + " Y" + string.Format("{0,1:F3}", Math.Round(yCenter + yCoords[1][0], 3)));
                        // Record the previous position
                        prevX = xCenter + xCoords[1][0];
                        prevY = yCenter + yCoords[1][0];
                        sw.WriteLine("G1 Z" + string.Format("{0,1:F3}", Math.Round(2 * layerHeight, 3)));
                        sw.WriteLine("G1 E0.80000 F2100.00000; ready filament");
                        sw.WriteLine("M204 S1000");
                        sw.WriteLine("G1 F" + string.Format("{0,1:F3}", printFeedRate) + " ; restore feed rate to that used for printing");

                        // Update progress display
                        minutesElapsed = homeAndCalibrateMinutes + 2 * introLineMinutes + (preDistanceFirstLayer / printFeedRate) + (step / oneMinuteInSteps);
                        minutesRemaining = (int)Math.Round(totalMinutes - minutesElapsed);
                        percentDone = (int)Math.Round(100 * minutesElapsed / totalMinutes);
                        sw.WriteLine("M73 Q" + percentDone.ToString() + " S" + minutesRemaining.ToString());
                        sw.WriteLine("M73 P" + percentDone.ToString() + " R" + minutesRemaining.ToString());
                    }
                }

                sw.WriteLine("G1 Z" + string.Format("{0,1:F3}", Math.Round((nLayers + 1) * layerHeight, 3)));
                sw.WriteLine("G1 E-0.80000 F2100.00000 ; retract filament");
                sw.WriteLine("G1 Z" + string.Format("{0,1:F3}", Math.Round((nLayers + 1) * layerHeight, 3) + 10) + " F10800.000 ; lift tip up 10 more mm");
                sw.WriteLine("");
                sw.WriteLine("M73 Q100 S0 ; updating progress display (100% done, 0 minutes remaining)");
                sw.WriteLine("M73 P100 R0 ; updating progress display (100% done, 0 minutes remaining)");
                sw.WriteLine("");
                sw.WriteLine("; Filament-specific end gcode");
                sw.WriteLine("M221 S100");
                sw.WriteLine("M104 S0 ; turn off temperature");
                sw.WriteLine("M140 S0 ; turn off heatbed");
                sw.WriteLine("M107 ; turn off fan");
                sw.WriteLine("G1 Z210 ; Move print head up to the top");
                sw.WriteLine("G1 X0 Y200; home X axis");
                sw.WriteLine("M84 ; disable motors");
            }
        }

        static void GenerateSierpinskiPyramid(int exponent, out List<double[]> xCoords, out List<double[]> yCoords, out List<int> layerIndexOffsets, out int sideLen)
        {
            // First make sure we have all necessary Sierpinski Curves
            if (exponent > PreComputedSierpinskiCurves.Count)
            {
                GenerateSierpinskiCurve(exponent, out _, out _, out _);
            }

            if (PreComputedSierpinskiPyramids.Count < 1)
            {
                PreComputedSierpinskiPyramids.Add(null); // There is no pyramid 0, but we set it, if necessary, so the length of the list will be correct
                PreComputedSierpinskiPyramidLayerIndexOffsets.Add(null);
            }

            // Handle degenerate cases (note that there is no pyramid 0 -- by design we skip this)
            if (exponent < 1)
            {
                sideLen = 0;
                xCoords = null;
                yCoords = null;
                layerIndexOffsets = null;
                return;
            }

            // Pre-compute any pyramids that have not yet been computed
            for (int exp = PreComputedSierpinskiPyramids.Count; exp <= exponent; exp++)
            {
                if (exp == 1) // base case
                {
                    List<double[]> xCoordsNewPyramid = new List<double[]>();
                    List<double[]> yCoordsNewPyramid = new List<double[]>();
                    List<int> layerIndexOffsetsNewPyramid = new List<int>();
                    for (int layerIndex = 0; layerIndex < layersPerPyramid; layerIndex++)
                    {
                        int curveIndex = ((3 * (layersPerPyramid - 1 - layerIndex)) / layersPerPyramid) - 1; // partition the smallest pyramid into "thirds"
                        if (curveIndex < 0)
                        {
                            curveIndex = 0;
                        }
                        int nCurvePts = PreComputedSierpinskiCurves[curveIndex].Item1.Length;
                        int[] xCoordsCurve = new int[nCurvePts];
                        int[] yCoordsCurve = new int[nCurvePts];
                        Array.Copy(PreComputedSierpinskiCurves[curveIndex].Item1, xCoordsCurve, nCurvePts);
                        Array.Copy(PreComputedSierpinskiCurves[curveIndex].Item2, yCoordsCurve, nCurvePts);
                        if (curveIndex == 0)
                        {
                            // Scale up the coordinates to overlay well with the curve at index 1
                            for (int i = 0; i < nCurvePts; i++)
                            {
                                xCoordsCurve[i] *= 2;
                                yCoordsCurve[i] *= 2;
                            }
                        }

                        double scaleFactor = (layersPerPyramid - layerIndex) / (double)layersPerPyramid; // scale factor due to the pyramid tapering
                        double[] xCoordsLayer = new double[nCurvePts];
                        double[] yCoordsLayer = new double[nCurvePts];
                        for (int i = 0; i < nCurvePts; i++)
                        {
                            xCoordsLayer[i] = 0.25 * scaleFactor * gridStep * xCoordsCurve[i]; // note: we multiply by 0.25 since curve coords are specified in quarter grid-steps
                            yCoordsLayer[i] = 0.25 * scaleFactor * gridStep * yCoordsCurve[i];
                        }

                        xCoordsNewPyramid.Add(xCoordsLayer);
                        yCoordsNewPyramid.Add(yCoordsLayer);

                        layerIndexOffsetsNewPyramid.Add((nCurvePts / 2) + ((nCurvePts / 4 + 1) / 2));
                    }

                    PreComputedSierpinskiPyramids.Add(Tuple.Create(xCoordsNewPyramid, yCoordsNewPyramid));
                    PreComputedSierpinskiPyramidLayerIndexOffsets.Add(layerIndexOffsetsNewPyramid);
                }
                else // exp > 1
                {
                    int totalLayers = (1 << (exp - 1)) * layersPerPyramid;
                    List<double[]> xCoordsNewPyramid = new List<double[]>();
                    List<double[]> yCoordsNewPyramid = new List<double[]>();
                    List<int> layerIndexOffsetsNewPyramid = new List<int>();

                    // By this point, this call should just return us the pre-computed data
                    GenerateSierpinskiPyramid(exp - 1, out xCoords, out yCoords, out layerIndexOffsets, out _);

                    // Start with the bottom half
                    for (int layerIndex = 0; layerIndex < totalLayers / 2; layerIndex++)
                    {
                        int invertedLayerIndex = totalLayers / 2 - 1 - layerIndex; // layer index needed for the upside-down pyramid

                        // Compute number of vertices in the current subcurve
                        int nCurvePts = 4 * (
                            xCoords[layerIndex].Length
                            + (layerIndex == 0 ? 1 : 0))
                            + (layerIndex > 0 ? xCoords[invertedLayerIndex].Length : 0);

                        double[] xCoordsLayer = new double[nCurvePts];
                        double[] yCoordsLayer = new double[nCurvePts];

                        // Circularly shift the index positions so we're starting where we need to be
                        int nSmall = xCoords[layerIndex].Length;
                        double[] xCoordsSmallReIndexed = new double[nSmall];
                        double[] yCoordsSmallReIndexed = new double[nSmall];
                        Array.Copy(xCoords[layerIndex], xCoordsSmallReIndexed, nSmall);
                        Array.Copy(yCoords[layerIndex], yCoordsSmallReIndexed, nSmall);
                        int newStartIndex = layerIndexOffsets[layerIndex];
                        CircShift(xCoordsSmallReIndexed, yCoordsSmallReIndexed, newStartIndex);

                        // Note that we deliberately _don't_ shift the inverted pyramid
                        int nSmallInverted = 4;
                        double[] xCoordsSmallInverted = new double[] { -2 * 0.25 * gridStep, 0, 2 * 0.25 * gridStep, 0 };
                        double[] yCoordsSmallInverted = new double[] { 0, -2 * 0.25 * gridStep, 0, 2 * 0.25 * gridStep };
                        if (layerIndex > 0)
                        {
                            // Create a copy of the layer from the inverted pyramid
                            nSmallInverted = xCoords[invertedLayerIndex].Length;
                            xCoordsSmallInverted = new double[nSmallInverted];
                            yCoordsSmallInverted = new double[nSmallInverted];
                            Array.Copy(xCoords[invertedLayerIndex], xCoordsSmallInverted, nSmallInverted);
                            Array.Copy(yCoords[invertedLayerIndex], yCoordsSmallInverted, nSmallInverted);
                        }

                        // Begin by copying just the first eighth of the upside-down pyramid to the current layer
                        int nFirstEigth = (nSmallInverted / 4 - 1) / 2 + 1;
                        Array.Copy(xCoordsSmallInverted, 0, xCoordsLayer,
                            0, nFirstEigth);
                        Array.Copy(yCoordsSmallInverted, 0, yCoordsLayer,
                            0, nFirstEigth);

                        // Next, make a copy of the smaller pyramid to go on the lower-left
                        double offset = (1 << exp) * 0.25 * gridStep;
                        CopyWithOffset(xCoordsSmallReIndexed, yCoordsSmallReIndexed, 0, xCoordsLayer, yCoordsLayer,
                            nFirstEigth, nSmall, -offset, -offset);

                        // Then go through the next fourth of the upside-down pyramid
                        Array.Copy(xCoordsSmallInverted, nFirstEigth, xCoordsLayer,
                            nFirstEigth + nSmall, nSmallInverted / 4);
                        Array.Copy(yCoordsSmallInverted, nFirstEigth, yCoordsLayer,
                            nFirstEigth + nSmall, nSmallInverted / 4);

                        // Next, make a copy of the smaller pyramid to go on the lower-right
                        CopyWithRotationAndOffset(xCoordsSmallReIndexed, yCoordsSmallReIndexed, 0, xCoordsLayer, yCoordsLayer,
                            nFirstEigth + nSmall + nSmallInverted / 4, nSmall, offset, -offset, 1);

                        // Then go through the next fourth of the upside-down pyramid
                        Array.Copy(xCoordsSmallInverted, nFirstEigth + nSmallInverted / 4, xCoordsLayer,
                            nFirstEigth + 2 * nSmall + nSmallInverted / 4, nSmallInverted / 4);
                        Array.Copy(yCoordsSmallInverted, nFirstEigth + nSmallInverted / 4, yCoordsLayer,
                            nFirstEigth + 2 * nSmall + nSmallInverted / 4, nSmallInverted / 4);

                        // Next, make a copy of the smaller pyramid to go on the upper-right
                        CopyWithRotationAndOffset(xCoordsSmallReIndexed, yCoordsSmallReIndexed, 0, xCoordsLayer, yCoordsLayer,
                            nFirstEigth + 2 * nSmall + nSmallInverted / 2, nSmall, offset, offset, 2);

                        // Upper-right pyramid is also the one we need to compute the new layer offsets based on
                        // (note that we don't actually use layerIndexOffsets[layerIndex], since the indices will have already been rotated)
                        layerIndexOffsetsNewPyramid.Add(nFirstEigth + 2 * nSmall + nSmallInverted / 2 + nSmall / 2);

                        // Then go through the next fourth of the upside-down pyramid
                        Array.Copy(xCoordsSmallInverted, nFirstEigth + nSmallInverted / 2, xCoordsLayer,
                            nFirstEigth + 3 * nSmall + nSmallInverted / 2, nSmallInverted / 4);
                        Array.Copy(yCoordsSmallInverted, nFirstEigth + nSmallInverted / 2, yCoordsLayer,
                            nFirstEigth + 3 * nSmall + nSmallInverted / 2, nSmallInverted / 4);

                        // Next, make a copy of the smaller pyramid to go on the upper-left
                        CopyWithRotationAndOffset(xCoordsSmallReIndexed, yCoordsSmallReIndexed, 0, xCoordsLayer, yCoordsLayer,
                            nFirstEigth + 3 * nSmall + 3 * nSmallInverted / 4, nSmall, -offset, offset, 3);

                        // Then go through the rest of the upside-down pyramid
                        Array.Copy(xCoordsSmallInverted, nFirstEigth + 3 * nSmallInverted / 4, xCoordsLayer,
                            nFirstEigth + 4 * nSmall + 3 * nSmallInverted / 4, nSmallInverted - (nFirstEigth + 3 * nSmallInverted / 4));
                        Array.Copy(yCoordsSmallInverted, nFirstEigth + 3 * nSmallInverted / 4, yCoordsLayer,
                            nFirstEigth + 4 * nSmall + 3 * nSmallInverted / 4, nSmallInverted - (nFirstEigth + 3 * nSmallInverted / 4));

                        // Add layer
                        xCoordsNewPyramid.Add(xCoordsLayer);
                        yCoordsNewPyramid.Add(yCoordsLayer);
                    }

                    // Next, generate the top half (basically just a copy of the previous pyramid)
                    for (int layerIndex = totalLayers / 2; layerIndex < totalLayers; layerIndex++)
                    {
                        int oldLayerIndex = layerIndex - totalLayers / 2;

                        int nCurvePts = xCoords[oldLayerIndex].Length;

                        double[] xCoordsLayer = new double[nCurvePts];
                        double[] yCoordsLayer = new double[nCurvePts];
                        Array.Copy(xCoords[oldLayerIndex], xCoordsLayer, nCurvePts);
                        Array.Copy(yCoords[oldLayerIndex], yCoordsLayer, nCurvePts);
                        int layerIndexOffset = layerIndexOffsets[oldLayerIndex];

                        // Add layer
                        xCoordsNewPyramid.Add(xCoordsLayer);
                        yCoordsNewPyramid.Add(yCoordsLayer);
                        layerIndexOffsetsNewPyramid.Add(layerIndexOffset);
                    }

                    PreComputedSierpinskiPyramids.Add(Tuple.Create(xCoordsNewPyramid, yCoordsNewPyramid));
                    PreComputedSierpinskiPyramidLayerIndexOffsets.Add(layerIndexOffsetsNewPyramid);
                }
            }

            // Compute out argument(s) and copy pre-computed data
            sideLen = 1 << exponent;
            xCoords = new List<double[]>();
            yCoords = new List<double[]>();
            layerIndexOffsets = new List<int>();
            int numLayers = PreComputedSierpinskiPyramids[exponent].Item1.Count;
            for (int layerNumber = 0; layerNumber < numLayers; layerNumber++)
            {
                int nCurvePts = PreComputedSierpinskiPyramids[exponent].Item1[layerNumber].Length;
                double[] xCoordsLayer = new double[nCurvePts];
                double[] yCoordsLayer = new double[nCurvePts];
                Array.Copy(PreComputedSierpinskiPyramids[exponent].Item1[layerNumber], xCoordsLayer, nCurvePts);
                Array.Copy(PreComputedSierpinskiPyramids[exponent].Item2[layerNumber], yCoordsLayer, nCurvePts);
                xCoords.Add(xCoordsLayer);
                yCoords.Add(yCoordsLayer);
                layerIndexOffsets.Add(PreComputedSierpinskiPyramidLayerIndexOffsets[exponent][layerNumber]);
            }
        }

        static void GenerateSierpinskiCurve(int exponent, out int[] xCoords, out int[] yCoords, out int sideLen)
        {
            // Handle degenerate cases
            if (exponent < 0)
            {
                sideLen = 0;
                if (exponent == -1)
                {
                    xCoords = new int[] { 0 };
                    yCoords = new int[] { 0 };
                }
                else
                {
                    xCoords = new int[] { };
                    yCoords = new int[] { };
                }
                return;
            }

            // Note that the integer coordinates computed here are in terms of quarter-steps

            // Pre-compute any curves that have not yet been computed
            for (int exp = PreComputedSierpinskiCurves.Count; exp <= exponent; exp++)
            {
                if (exp == 0)
                {
                    PreComputedSierpinskiCurves.Add(Tuple.Create(
                        new int[] { -1, 0, 1, 0 },
                        new int[] { 0, -1, 0, 1 }));
                }
                else
                {
                    // Compute number of vertices in the current subcurve
                    int n = 4;
                    for (int i = 0; i < exp; i++)
                    {
                        n = 4 * (n + 1);
                    }

                    // Allocate coordinate arrays for subcurves
                    int[] xCoordsSubCurve = new int[n];
                    int[] yCoordsSubCurve = new int[n];

                    // By this point, this call should just return us the pre-computed data
                    GenerateSierpinskiCurve(exp - 1, out xCoords, out yCoords, out _);
                    int nSmall = xCoords.Length;

                    // Circularly shift the index positions so we're starting where we need to be
                    int newStartIndex = (nSmall / 2) + ((nSmall / 4 + 1) / 2);
                    CircShift(xCoords, yCoords, newStartIndex);

                    // Vertex just before connecting to the lower-left sub-square
                    xCoordsSubCurve[0] = -2;
                    yCoordsSubCurve[0] = 0;

                    // Copy the smaller Sierpinski Curve into this one with the appropriate offset
                    int offset = 1 << exp;
                    CopyWithOffset(xCoords, yCoords, 0, xCoordsSubCurve, yCoordsSubCurve, 1, nSmall, -offset, -offset);

                    xCoordsSubCurve[nSmall + 1] = 0;
                    yCoordsSubCurve[nSmall + 1] = -2;

                    // Copy the smaller Sierpinski Curve into this one with the appropriate offset
                    CopyWithRotationAndOffset(xCoords, yCoords, 0, xCoordsSubCurve, yCoordsSubCurve, 2 + nSmall, nSmall, offset, -offset, 1);

                    xCoordsSubCurve[2 * (nSmall + 1)] = 2;
                    yCoordsSubCurve[2 * (nSmall + 1)] = 0;

                    // Copy the smaller Sierpinski Curve into this one with the appropriate offset
                    CopyWithRotationAndOffset(xCoords, yCoords, 0, xCoordsSubCurve, yCoordsSubCurve, 3 + 2 * nSmall, nSmall, offset, offset, 2);

                    xCoordsSubCurve[3 * (nSmall + 1)] = 0;
                    yCoordsSubCurve[3 * (nSmall + 1)] = 2;

                    // Copy the smaller Sierpinski Curve into this one with the appropriate offset
                    CopyWithRotationAndOffset(xCoords, yCoords, 0, xCoordsSubCurve, yCoordsSubCurve, 4 + 3 * nSmall, nSmall, -offset, offset, 3);

                    // Store in pre-computed curves
                    PreComputedSierpinskiCurves.Add(Tuple.Create(xCoordsSubCurve, yCoordsSubCurve));
                }
            }

            // Compute out argument(s) and copy pre-computed data
            sideLen = 1 << exponent;
            int totalPts = 4;
            for (int i = 0; i < exponent; i++)
            {
                totalPts = 4 * (totalPts + 1);
            }
            xCoords = new int[totalPts];
            yCoords = new int[totalPts];
            Array.Copy(PreComputedSierpinskiCurves[exponent].Item1, xCoords, totalPts);
            Array.Copy(PreComputedSierpinskiCurves[exponent].Item2, yCoords, totalPts);
        }

        static void CircShift(int[] xCoords, int[] yCoords, int startIdx)
        {
            int n = xCoords.Length;
            int[] xTemp = new int[n];
            int[] yTemp = new int[n];
            Array.Copy(xCoords, xTemp, n);
            Array.Copy(yCoords, yTemp, n);
            for (int i = 0; i < n; i++)
            {
                xCoords[i] = xTemp[(i + startIdx) % n];
                yCoords[i] = yTemp[(i + startIdx) % n];
            }
        }

        static void CircShift(double[] xCoords, double[] yCoords, int startIdx)
        {
            int n = xCoords.Length;
            double[] xTemp = new double[n];
            double[] yTemp = new double[n];
            Array.Copy(xCoords, xTemp, n);
            Array.Copy(yCoords, yTemp, n);
            for (int i = 0; i < n; i++)
            {
                xCoords[i] = xTemp[(i + startIdx) % n];
                yCoords[i] = yTemp[(i + startIdx) % n];
            }
        }

        static void CopyWithOffset(int[] sourceX, int[] sourceY, int sourceStartIndex,
            int[] destinationX, int[] destinationY, int destinationStartIndex,
            int length, int offsetX, int offsetY)
        {
            Array.Copy(sourceX, sourceStartIndex, destinationX, destinationStartIndex, length);
            Array.Copy(sourceY, sourceStartIndex, destinationY, destinationStartIndex, length);
            for (int i = 0; i < length; i++)
            {
                destinationX[destinationStartIndex + i] += offsetX;
                destinationY[destinationStartIndex + i] += offsetY;
            }
        }

        static void CopyWithOffset(double[] sourceX, double[] sourceY, int sourceStartIndex,
            double[] destinationX, double[] destinationY, int destinationStartIndex,
            int length, double offsetX, double offsetY)
        {
            Array.Copy(sourceX, sourceStartIndex, destinationX, destinationStartIndex, length);
            Array.Copy(sourceY, sourceStartIndex, destinationY, destinationStartIndex, length);
            for (int i = 0; i < length; i++)
            {
                destinationX[destinationStartIndex + i] += offsetX;
                destinationY[destinationStartIndex + i] += offsetY;
            }
        }

        static void CopyWithRotationAndOffset(int[] sourceX, int[] sourceY, int sourceStartIndex,
            int[] destinationX, int[] destinationY, int destinationStartIndex,
            int length, int offsetX, int offsetY,
            int num90DegreeRotations)
        {
            Array.Copy(sourceX, sourceStartIndex, destinationX, destinationStartIndex, length);
            Array.Copy(sourceY, sourceStartIndex, destinationY, destinationStartIndex, length);
            for (int i = 0; i < length; i++)
            {
                // rotate entire pattern counterclockwise by 90 * num90DegreeRotations degrees, then offset
                // rotatedX = x cos(t) - y sin(t)
                // rotatedY = x sin(t) + y cos(t)
                int xInit = destinationX[destinationStartIndex + i];
                int yInit = destinationY[destinationStartIndex + i];
                int rotatedX = xInit * cosines[num90DegreeRotations % 4] - yInit * sines[num90DegreeRotations % 4];
                int rotatedY = xInit * sines[num90DegreeRotations % 4] + yInit * cosines[num90DegreeRotations % 4];
                destinationX[destinationStartIndex + i] = rotatedX + offsetX;
                destinationY[destinationStartIndex + i] = rotatedY + offsetY;
            }
        }

        static void CopyWithRotationAndOffset(double[] sourceX, double[] sourceY, int sourceStartIndex,
            double[] destinationX, double[] destinationY, int destinationStartIndex,
            int length, double offsetX, double offsetY,
            int num90DegreeRotations)
        {
            Array.Copy(sourceX, sourceStartIndex, destinationX, destinationStartIndex, length);
            Array.Copy(sourceY, sourceStartIndex, destinationY, destinationStartIndex, length);
            for (int i = 0; i < length; i++)
            {
                // rotate entire pattern counterclockwise by 90 * num90DegreeRotations degrees, then offset
                // rotatedX = x cos(t) - y sin(t)
                // rotatedY = x sin(t) + y cos(t)
                double xInit = destinationX[destinationStartIndex + i];
                double yInit = destinationY[destinationStartIndex + i];
                double rotatedX = xInit * cosines[num90DegreeRotations % 4] - yInit * sines[num90DegreeRotations % 4];
                double rotatedY = xInit * sines[num90DegreeRotations % 4] + yInit * cosines[num90DegreeRotations % 4];
                destinationX[destinationStartIndex + i] = rotatedX + offsetX;
                destinationY[destinationStartIndex + i] = rotatedY + offsetY;
            }
        }

        // See https://en.wikipedia.org/wiki/Hilbert_curve

        //convert (x,y) to d
        static int xy2d(int n, int x, int y)
        {
            int rx, ry, s, d = 0;
            for (s = n / 2; s > 0; s /= 2)
            {
                rx = (x & s) > 0 ? 1 : 0;
                ry = (y & s) > 0 ? 1 : 0;
                d += s * s * ((3 * rx) ^ ry);
                rot(n, ref x, ref y, rx, ry);
            }
            return d;
        }

        //convert d to (x,y)
        static void d2xy(int n, int d, ref int x, ref int y)
        {
            int rx, ry, s, t = d;
            x = 0;
            y = 0;
            for (s = 1; s < n; s *= 2)
            {
                rx = 1 & (t / 2);
                ry = 1 & (t ^ rx);
                rot(s, ref x, ref y, rx, ry);
                x += s * rx;
                y += s * ry;
                t /= 4;
            }
        }

        //rotate/flip a quadrant appropriately
        static void rot(int n, ref int x, ref int y, int rx, int ry)
        {
            if (ry == 0)
            {
                if (rx == 1)
                {
                    x = n - 1 - x;
                    y = n - 1 - y;
                }

                //Swap x and y
                int t = x;
                x = y;
                y = t;
            }
        }
    }
}

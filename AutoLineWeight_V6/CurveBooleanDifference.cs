﻿using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;

namespace AutoLineWeight_V6
{
    public class CurveBooleanDifference : Command
    {
        // initialize curve selection
        Curve srcCrv;
        Curve[] crvSet;

        List<Curve> resultCurves = new List<Curve>();
        List<Curve> overlaps = new List<Curve>();

        public CurveBooleanDifference(Curve srcCrv, Curve[] crvSet)
        {
            this.srcCrv = srcCrv;
            this.crvSet = crvSet;
            Instance = this;
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static CurveBooleanDifference Instance { get; private set; }

        public override string EnglishName => "CurveBooleanDifference";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (srcCrv == null) { return Result.Cancel; }

            BoundingBox bb1 = srcCrv.GetBoundingBox(false);

            double startParam;
            srcCrv.LengthParameter(0, out startParam);
            double endParam;
            srcCrv.LengthParameter(srcCrv.GetLength(), out endParam);

            Interval fromInterval = new Interval(startParam, endParam);

            List<Interval> remainingIntervals = new List<Interval> { fromInterval };
            List<Interval> overlapIntervals = new List<Interval>();

            // loops through each curve with wich to subtract from srcCrv
            foreach (Curve crv in crvSet)
            {
                if (crv == null) { continue; }

                // only calculate intersections if bounding boxes coincide
                BoundingBox bb2 = crv.GetBoundingBox(false);
                if (!BoundingBoxOperations.BoundingBoxCoincides(bb1, bb2)) { continue; }

                // calculate curve curve intersection
                double tol = doc.ModelAbsoluteTolerance;
                CurveIntersections intersections = Intersection.CurveCurve(srcCrv, crv, tol, tol);

                // subtract interval generated by intersection from remaining intervals
                for (int i = 0; i < intersections.Count; i++)
                {
                    IntersectionEvent intersection = intersections[i];
                    if (intersection == null) continue;
                    if (intersection.IsOverlap)
                    {
                        Interval overlap = intersection.OverlapA;
                        overlapIntervals.Add(overlap);
                        remainingIntervals = IntervalDifference(remainingIntervals, overlap);
                    }
                }
            }

            foreach (Interval interval in remainingIntervals)
            {
                Curve trimmed = srcCrv.Trim(interval);
                resultCurves.Add(trimmed);
            }

            List<Interval> cleanedOverlaps = MergeOverlappingIntervals(overlapIntervals);

            foreach (Interval interval in cleanedOverlaps)
            {
                Curve trimmed = srcCrv.Trim(interval);
                overlaps.Add(trimmed);
            }
            return Result.Success;
        }

        private List<Interval> IntervalDifference(List<Interval> intervals, Interval toRemove)
        {
            List<Interval> remaining = new List<Interval>();
            for (int i = 0; i < intervals.Count; i++)
            {
                Interval interval = intervals[i];
                interval.MakeIncreasing();

                if (interval.Min >= toRemove.Max || interval.Max <= toRemove.Min)
                {
                    remaining.Add(interval);
                    continue;
                }
                if (interval.Min < toRemove.Min)
                {
                    remaining.Add(new Interval(interval.Min, Math.Min(toRemove.Min, interval.Max)));
                }

                if (interval.Max > toRemove.Max)
                {
                    remaining.Add(new Interval(Math.Max(toRemove.Max, interval.Min), interval.Max));
                }
            }
            return remaining;
        }

        public void CalculateOverlap()
        {
            RunCommand(RhinoDoc.ActiveDoc, RunMode.Interactive);
        }

        private List<Interval> MergeOverlappingIntervals(List<Interval> intervals)
        {
            if (intervals.Count <= 1)
            {
                return intervals;
            }

            intervals.Sort((x, y) => x.Min.CompareTo(y.Min));

            List<Interval> mergedIntervals = new List<Interval> { intervals[0] };

            for (int i = 1; i < intervals.Count; i++)
            {
                Interval current = intervals[i];
                Interval previous = mergedIntervals[mergedIntervals.Count - 1];

                if (current.Min <= previous.Max)
                {
                    mergedIntervals[mergedIntervals.Count - 1] = new Interval(previous.Min, Math.Max(previous.Max, current.Max));
                }
                else
                {
                    mergedIntervals.Add(current);
                }
            }

            return mergedIntervals;
        }

        public Curve[] GetResultCurves()
        {
            return this.resultCurves.ToArray();
        }

        public Curve[] GetOverlapCurves()
        {
            return this.overlaps.ToArray();
        }
    }
}
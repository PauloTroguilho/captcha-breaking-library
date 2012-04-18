﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScottClayton.Neural;
using System.IO;
using System.ComponentModel;
using ScottClayton.Utility;
using ScottClayton.CAPTCHA.Image;
using ScottClayton.CAPTCHA.Utility;

namespace ScottClayton.CAPTCHA
{
    public class BitmapSubtractionSolver : Solver
    {
        private List<string> charsSet;

        private BitmapVectorCollection solver;

        private BackgroundWorker trainerWorker;
        private BackgroundWorker testerWorker;

        private bool merge;

        public override List<string> CharacterSet { get { return charsSet; } protected set { charsSet = value; } }

        public BitmapSubtractionSolver(string characterSet, int imageWidth, int imageHeight)
            : this(characterSet, imageWidth, imageHeight, true)
        {
        }

        public BitmapSubtractionSolver(string characterSet, int imageWidth, int imageHeight, bool mergePatterns)
        {
            // Split the set of characters into a list
            charsSet = characterSet.ToCharStringList();

            ExpectedWidth = imageWidth;
            ExpectedHeight = imageHeight;

            merge = mergePatterns;

            solver = new BitmapVectorCollection();

            trainerWorker = new BackgroundWorker();
            trainerWorker.DoWork += new DoWorkEventHandler(worker_DoWork);
            trainerWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            trainerWorker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            trainerWorker.WorkerReportsProgress = true;

            testerWorker = new BackgroundWorker();
            testerWorker.DoWork += new DoWorkEventHandler(testerWorker_DoWork);
            testerWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(testerWorker_RunWorkerCompleted);
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            RaiseOnTrainingProgressChanged(new OnTrainingProgressChangeEventArgs(e.ProgressPercentage, (double)e.UserState));
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            RaiseOnTrainingComplete((PatternResult)e.Result);
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            StartTrainingArgs args = (StartTrainingArgs)e.Argument;
            e.Result = Train(args.Patterns, args.Iterations);
        }

        void sann_OnTrainingProgressChange(object sender, OnTrainingProgressChangeEventArgs e)
        {
            trainerWorker.ReportProgress(e.Progress, e.Error);
        }

        void testerWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            RaiseOnTrainingComplete((PatternResult)e.Result);
        }

        void testerWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = Test((List<Pattern>)e.Argument);
        }

        public override string Solve(List<Pattern> patterns)
        {
            //((BitmapVector)patterns[0].Inputs).GetBitmap().Save("xpattern.bmp"); // TEMP TEST
            //GlobalMessage.SendMessage(((BitmapVector)patterns[0].Inputs).GetBitmap());

            // Convert every output to a character in the solution
            return patterns.Select(p => GetOutputCharacter(p)).Aggregate((c,n) => c + n);
        }

        private string GetOutputCharacter(Pattern p)
        {
            return solver.GetClosestMatch((BitmapVector)p.Inputs).Information;
        }

        public override PatternResult Train(List<Pattern> patterns, int iterations)
        {
            foreach (Pattern p in patterns)
            {
                BitmapVector vector = (BitmapVector)p.Inputs;
                vector.Information = GetOutputCharacter(p.Outputs);

                if (merge)
                {
                    // Combine all patterns with the same solution
                    solver.AddToGroup(vector);
                }
                else
                {
                    // Don't combine all patterns with the same letter
                    solver.Add(vector);
                }
            }

            // Uncomment this line if you would like to see what the really cool pattern bitmaps look like
            solver.ExportAllVectorsAsBitmaps("ztest");

            GlobalMessage.SendMessage(solver.GetAllVectorsAsBitmaps());

            return new PatternResult();
        }

        private string GetOutputCharacter(DoubleVector v)
        {
            // There is one output for each possible character the CAPTCHA can contain.
            // The index of the output with the highest value corresponds to a character in the character set.
            return charsSet[v.GetIndexOfLargestElement()];
        }

        public override void TrainAsync(List<Pattern> patterns, int iterations)
        {
            if (!trainerWorker.IsBusy)
            {
                trainerWorker.RunWorkerAsync(new StartTrainingArgs() { Patterns = patterns, Iterations = iterations });
            }
        }

        public override PatternResult Test(List<Pattern> patterns)
        {
            double correct = 0;

            foreach (Pattern p in patterns)
            {
                //((BitmapVector)p.Inputs).GetBitmap().Save("asdfasdf.bmp");
                //string t1 = solver.GetClosestMatch((BitmapVector)p.Inputs).Information;
                //string t2 = GetOutputCharacter(p.Outputs);

                if (solver.GetClosestMatch((BitmapVector)p.Inputs).Information == GetOutputCharacter(p.Outputs))
                {
                    correct++;
                }
            }

            return new PatternResult(0.0, (correct / patterns.Count) * 100.0);
        }

        public override void TestAsync(List<Pattern> patterns)
        {
            if (!testerWorker.IsBusy)
            {
                testerWorker.RunWorkerAsync(patterns);
            }
        }

        public override void Save(BinaryWriter w)
        {
            base.Save(w);
            w.Write(charsSet.Aggregate((c, n) => c + n));
            solver.SaveToFile(w);
        }

        public override void Load(BinaryReader r)
        {
            base.Load(r);
            charsSet = r.ReadString().ToCharArray().ToList().Select(c => c.ToString()).ToList();
            solver = BitmapVectorCollection.LoadFromFile(r);
        }
    }
}

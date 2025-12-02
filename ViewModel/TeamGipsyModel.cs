using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GalaSoft.MvvmLight.CommandWpf;
using TeamGipsy.Model.PushControl;
using TeamGipsy.Model.Download;
using TeamGipsy.Model.Mp3;
using TeamGipsy.ViewModel;
using System.Windows.Forms;
using TeamGipsy.Model.SqliteControl;
using TeamGipsy.Model.SM2plus;

namespace TeamGipsy.ViewModel
{
    public class ToastFishModel : ViewModelBase
    {
        public ToastFishModel()
        {
            Push = new RelayCommand(PushTest);
            RefreshDashboard = new RelayCommand(ComputeDashboard);
        }

        public NotifyIcon notifyIcon;

        public ICommand Push { get; set; }
        public ICommand RefreshDashboard { get; set; }

        private int _todayCount;
        private int _weekCount;
        private int _reviewCompleted;
        private double _accuracyRate;
        private int _streakDays;

        public int TodayCount
        {
            get { return _todayCount; }
            set { _todayCount = value; RaisePropertyChanged(() => TodayCount); }
        }

        public int WeekCount
        {
            get { return _weekCount; }
            set { _weekCount = value; RaisePropertyChanged(() => WeekCount); }
        }

        public int ReviewCompleted
        {
            get { return _reviewCompleted; }
            set { _reviewCompleted = value; RaisePropertyChanged(() => ReviewCompleted); }
        }

        public double AccuracyRate
        {
            get { return _accuracyRate; }
            set { _accuracyRate = value; RaisePropertyChanged(() => AccuracyRate); }
        }

        public int StreakDays
        {
            get { return _streakDays; }
            set { _streakDays = value; RaisePropertyChanged(() => StreakDays); }
        }

        private void PushTest()
        {
        }

        private void ComputeDashboard()
        {
            var sel = new Select();
            sel.SelectWordList();
            var allWords = sel.AllWordList != null ? sel.AllWordList.ToList() : new List<Word>();

            DateTime today = DateTime.Now.Date;
            var todayWords = new List<Word>();
            foreach (var w in allWords)
            {
                if (!string.IsNullOrEmpty(w.dateLastReviewed))
                {
                    DateTime dt;
                    if (DateTime.TryParse(w.dateLastReviewed, out dt))
                    {
                        if (dt.Date == today)
                            todayWords.Add(w);
                    }
                }
            }

            TodayCount = todayWords.Count;
            int reviewedExistingToday = 0;
            foreach (var w in todayWords)
            {
                if (!string.IsNullOrEmpty(w.dateFirstReviewed))
                {
                    DateTime dt0;
                    if (DateTime.TryParse(w.dateFirstReviewed, out dt0))
                    {
                        if (dt0.Date < today)
                            reviewedExistingToday++;
                    }
                    else
                    {
                        reviewedExistingToday++;
                    }
                }
            }
            ReviewCompleted = reviewedExistingToday;

            DateTime weekStart = GetWeekStart(today);
            int weekCnt = 0;
            foreach (var w in allWords)
            {
                if (!string.IsNullOrEmpty(w.dateLastReviewed))
                {
                    DateTime dt;
                    if (DateTime.TryParse(w.dateLastReviewed, out dt))
                    {
                        if (dt.Date >= weekStart && dt.Date <= today)
                            weekCnt++;
                    }
                }
            }
            WeekCount = weekCnt;

            int correctToday = 0;
            foreach (var w in todayWords)
            {
                if (w.lastScore >= Parameters.Correct)
                    correctToday++;
            }
            AccuracyRate = TodayCount == 0 ? 0 : Math.Round(100.0 * correctToday / TodayCount, 2);

            StreakDays = ComputeStreakDays(allWords, today);
        }

        private static DateTime GetWeekStart(DateTime date)
        {
            int diff = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
            if (diff < 0) diff += 7;
            return date.AddDays(-diff);
        }

        private static int ComputeStreakDays(List<Word> allWords, DateTime today)
        {
            var days = new HashSet<DateTime>();
            foreach (var w in allWords)
            {
                if (!string.IsNullOrEmpty(w.dateLastReviewed))
                {
                    DateTime dt;
                    if (DateTime.TryParse(w.dateLastReviewed, out dt))
                        days.Add(dt.Date);
                }
            }
            if (!days.Contains(today))
                return 0;
            int streak = 0;
            DateTime cursor = today;
            while (days.Contains(cursor))
            {
                streak++;
                cursor = cursor.AddDays(-1);
            }
            return streak;
        }
    }
}

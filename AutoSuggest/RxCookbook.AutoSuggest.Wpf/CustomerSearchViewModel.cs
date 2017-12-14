using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using RxCookbook.AutoSuggest.Wpf.Annotations;

namespace RxCookbook.AutoSuggest.Wpf
{
    public class CustomerSearchViewModel : INotifyPropertyChanged, IDisposable
    {
        private string _searchText;
        private ViewModelStatus _status;
        private readonly SerialDisposable _searchSubscription = new SerialDisposable();
        private readonly ICancelable _resources;

        public CustomerSearchViewModel()
        {
            SearchResults = new ObservableCollection<string>();
            Status = ViewModelStatus.Idle;
            
            //SearchResults.Add("Foo");
            //SearchResults.Add("Bar");
            //SearchText = "Is it here?";
            var textChangedSubscription = this.OnPropertyChanges(vm => vm.SearchText)
                .Throttle(TimeSpan.FromMilliseconds(100), DispatcherScheduler.Current)
                .Subscribe(ExecuteSearch);

            _resources = StableCompositeDisposable.Create(_searchSubscription, textChangedSubscription);

        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (value == _searchText) return;
                _searchText = value;
                OnPropertyChanged();
            }
        }

        public ViewModelStatus Status
        {
            get => _status;
            set
            {
                if (Equals(value, _status)) return;
                _status = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> SearchResults { get; }

        private void ExecuteSearch(string searchText)
        {
            //Set status processing
            Status = ViewModelStatus.Processing;

            //Cancel previous search (if still running) with new search
            _searchSubscription.Disposable = FindCustomers(searchText)
                .SubscribeOn(Scheduler.Default)
                .ObserveOn(DispatcherScheduler.Current)
                .Subscribe(
                    results =>
                    {
                        SearchResults.Clear();
                        foreach (var result in results)
                        {
                            SearchResults.Add(result);
                        }
                    },
                    ex =>
                    {
                        Status = ViewModelStatus.Error(ex.Message);
                    },
                    () =>
                    {
                        Status = ViewModelStatus.Idle;
                    });
        }

        private static readonly Random Rnd = new Random();


        private IObservable<string[]> FindCustomers(string searchText)
        {
            return Observable.Create<string[]>(obs =>
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    return Observable.Return(new string[0]).Subscribe(obs);
                }

                var delay = TimeSpan.FromMilliseconds(Rnd.Next(50, 750));
                var resultLen = Rnd.Next(1, 6);
                var results = new string[resultLen];
                for (int i = 0; i < resultLen; i++)
                {
                    results[i] = Rnd.NextDouble().ToString();
                }

                return Observable.Return(results)
                     .Delay(delay)
                     .Subscribe(obs);
            });
        }

        #region INPC Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        #endregion

        public void Dispose()
        {
            _resources?.Dispose();
        }
    }
}

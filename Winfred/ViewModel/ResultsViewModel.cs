using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winfred.ViewModel
{
    class ResultsViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<ResultViewModel> _Results;
        public ObservableCollection<ResultViewModel> Results
        {
            get { return _Results; }
            set { _Results = value; FirePropertyChanged("Results"); }
        }

        public ResultsViewModel()
        {
            _Results = new ObservableCollection< ResultViewModel>();
            _Results.CollectionChanged += _Results_CollectionChanged;
        }

        private void _Results_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            FirePropertyChanged("Results");
        }

        public virtual event PropertyChangedEventHandler PropertyChanged;

        public virtual void FirePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool FindByResultName(string targetResultName, out ResultViewModel targetResultViewModel)
        {
            foreach (ResultViewModel temp in _Results)
            {
                if (temp.ResultName == targetResultName)
                {
                    targetResultViewModel = temp;
                    return true;
                }
            }

            targetResultViewModel = new ResultViewModel();
            return false;
        }

        public void clear()
        {
            this.Results.Clear();
        }
    }
}

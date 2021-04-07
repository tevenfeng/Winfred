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
            set { _Results = value; FirePropertyChanged(nameof(Results)); }
        }

        private int _SelectedIndex;
        public int SelectedIndex
        {
            get { return _SelectedIndex; }
            set
            {
                _SelectedIndex = value;
                FirePropertyChanged(nameof(SelectedIndex));
            }
        }

        private ResultViewModel _SelectedResultViewModel;
        public ResultViewModel SelectedResultViewModel
        {
            get { return _SelectedResultViewModel; }
            set { _SelectedResultViewModel = value; FirePropertyChanged(nameof(SelectedResultViewModel)); }
        }

        public ResultsViewModel()
        {
            _Results = new ObservableCollection<ResultViewModel>();
            _SelectedIndex = -1;
            _Results.CollectionChanged += _Results_CollectionChanged;
        }

        public int SelectNext()
        {
            SelectedIndex = (SelectedIndex + 1) % _Results.Count;

            return SelectedIndex;
        }

        public int SelectPrevious()
        {
            if (SelectedIndex == 0)
            {
                SelectedIndex = _Results.Count - 1;
            }
            else
            {
                SelectedIndex--;
            }
            return SelectedIndex;
        }

        private void _Results_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            FirePropertyChanged(nameof(Results));
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

        public void Clear()
        {
            this.Results.Clear();
        }
    }
}

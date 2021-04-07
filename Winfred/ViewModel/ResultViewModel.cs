using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winfred.ViewModel
{
    enum ResultTypeEnum
    {
        Invalid = 0,
        Text = 1,
        Image = 2
    }

    class ResultViewModel : INotifyPropertyChanged
    {
        private string _ResultName;
        public string ResultName
        {
            get { return _ResultName; }
            set { _ResultName = value; FirePropertyChanged(nameof(ResultName)); }
        }

        private string _ResultPreview;
        public string ResultPreview
        {
            get { return _ResultPreview; }
            set { _ResultPreview = value; FirePropertyChanged(nameof(ResultPreview)); }
        }

        private ResultTypeEnum _MainTypeEnum;
        public ResultTypeEnum MainTypeEnum
        {
            get { return _MainTypeEnum; }
            set { _MainTypeEnum = value; FirePropertyChanged(nameof(MainTypeEnum)); }
        }

        private int _HashCode;
        public int HashCode
        {
            get { return _HashCode; }
            set { _HashCode = value; FirePropertyChanged(nameof(HashCode)); }
        }

        public ResultViewModel()
        {
            _ResultName = "";
            _ResultPreview = "";
            _MainTypeEnum = ResultTypeEnum.Invalid;
            _HashCode = 0;
        }

        public ResultViewModel(string ResultName, string ResultPreview, ResultTypeEnum resultTypeEnum, int HashCode)
        {
            _ResultName = ResultName;
            _ResultPreview = ResultPreview;
            _MainTypeEnum = resultTypeEnum;
            _HashCode = HashCode;
        }

        public virtual event PropertyChangedEventHandler PropertyChanged;

        public virtual void FirePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

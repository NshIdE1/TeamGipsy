using GalaSoft.MvvmLight;
using System.Windows.Input;
using GalaSoft.MvvmLight.CommandWpf;
using TeamGipsy.Model.PushControl;
using TeamGipsy.Model.Download;
using TeamGipsy.Model.Mp3;
using TeamGipsy.ViewModel;
using System.Windows.Forms;

namespace TeamGipsy.ViewModel
{
    public class ToastFishModel : ViewModelBase
    {
        public ToastFishModel()      
        {
            Push = new RelayCommand(PushTest);
        }
        public NotifyIcon notifyIcon;
        public ICommand Push { get; set; }

        private void PushTest()
        {
            //PushWords.Recitation(173);
        }
    }
}

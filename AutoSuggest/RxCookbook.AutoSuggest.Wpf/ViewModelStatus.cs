namespace RxCookbook.AutoSuggest.Wpf
{
    public sealed class ViewModelStatus
    {
        public bool IsProcessing { get; }
        public string ErrorMessage { get; }
        public static ViewModelStatus Idle = new ViewModelStatus(false);
        public static ViewModelStatus Processing = new ViewModelStatus(true);

        public static ViewModelStatus Error(string errorMessage)
        {
            return new ViewModelStatus(errorMessage);
        }

        private ViewModelStatus(bool isProcessing)
        {
            IsProcessing = isProcessing;
        }

        private ViewModelStatus(string errorMessage)
        {
            IsProcessing = false;
            ErrorMessage = errorMessage;
        }
    }
}
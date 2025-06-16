using System;
using System.ComponentModel;

namespace UchPR
{
    public class DocumentHistoryItem : INotifyPropertyChanged
    {
        private int _id;
        private string _documentName;
        private string _documentType;
        private string _status;
        private DateTime _createdDate;
        private DateTime _modifiedDate;
        private string _filePath;
        private long _fileSize;
        private string _fileSizeFormatted;
        private byte[] _documentData;
        private string _description;
        private string _createdBy;
        private string _modifiedBy;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string DocumentName
        {
            get => _documentName;
            set { _documentName = value; OnPropertyChanged(nameof(DocumentName)); }
        }

        public string DocumentType
        {
            get => _documentType;
            set { _documentType = value; OnPropertyChanged(nameof(DocumentType)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public DateTime CreatedDate
        {
            get => _createdDate;
            set { _createdDate = value; OnPropertyChanged(nameof(CreatedDate)); }
        }

        public DateTime ModifiedDate
        {
            get => _modifiedDate;
            set { _modifiedDate = value; OnPropertyChanged(nameof(ModifiedDate)); }
        }

        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(nameof(FilePath)); }
        }

        public long FileSize
        {
            get => _fileSize;
            set
            {
                _fileSize = value;
                _fileSizeFormatted = FormatFileSize(value);
                OnPropertyChanged(nameof(FileSize));
                OnPropertyChanged(nameof(FileSizeFormatted));
            }
        }

        public string FileSizeFormatted
        {
            get => _fileSizeFormatted;
            set { _fileSizeFormatted = value; OnPropertyChanged(nameof(FileSizeFormatted)); }
        }

        public byte[] DocumentData
        {
            get => _documentData;
            set { _documentData = value; OnPropertyChanged(nameof(DocumentData)); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        public string CreatedBy
        {
            get => _createdBy;
            set { _createdBy = value; OnPropertyChanged(nameof(CreatedBy)); }
        }

        public string ModifiedBy
        {
            get => _modifiedBy;
            set { _modifiedBy = value; OnPropertyChanged(nameof(ModifiedBy)); }
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 Б";

            string[] sizes = { "Б", "КБ", "МБ", "ГБ" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
    
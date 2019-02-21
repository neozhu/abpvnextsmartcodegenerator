using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace MadsKristensen.AddAnyFile
{
    public class AppServiceDialogViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
        public AppServiceDialogViewModel()
        {

        }
        private string rootNamesapce;
        private string modelName;
        private string dtoClass;
        private string cudtoClass;
        private string serviceClass;
        private string iserviceClass;
        private string subFolder;
        private string selectFolder;
        private string tips;
        private EntityInfo entity;

        private CollectionView entities;
        public CollectionView Entities
        {
            get => this.entities; set
            {

                this.entities = value;
                OnPropertyChanged("Entities");
            }
        }
        public EntityInfo Entity
        {
            get => this.entity; set
            {

                this.entity = value;
                OnPropertyChanged("Entity");
            }
        }
        public string Tips
        {
            get => this.tips; set
            {

                this.tips = value;
                OnPropertyChanged("Tips");
            }
        }
        public string ModelName { get => this.modelName; set {

                this.modelName = value;
                if (!string.IsNullOrEmpty(value)) {
                    this.DtoClass = $"{value}Dto";
                    this.CudtoClass = $"CreateUpdate{value}Dto";
                    this.ServiceClass = $"{value}AppService";
                    this.IServiceClass = $"I{value}AppService";
                    this.SubFolder= $"{value}/";
                }

                OnPropertyChanged("ModelName");
            } }

        public string DtoClass { get => this.dtoClass; set { this.dtoClass = value;
                OnPropertyChanged("DtoClass");
            } }
        public string RootNamespace
        {
            get => this.rootNamesapce; set
            {
                this.rootNamesapce = value;
                OnPropertyChanged("RootNamespace");
            }
        }
        public string CudtoClass { get => this.cudtoClass; set {
                this.cudtoClass = value;
                OnPropertyChanged("CudtoClass");
            } }
        public string ServiceClass { get => this.serviceClass; set {
                this.serviceClass = value;
                OnPropertyChanged("ServiceClass");
            } }
        public string IServiceClass
        {
            get => this.iserviceClass; set
            {
                this.iserviceClass = value;
                OnPropertyChanged("IServiceClass");
            }
        }
        public string SubFolder
        {
            get => this.subFolder; set
            {
                this.subFolder = value;
                OnPropertyChanged("SubFolder");
            }
        }
        public string SelectFolder
        {
            get => this.selectFolder; set
            {
                this.selectFolder = value;
                OnPropertyChanged("SelectFolder");
            }
        }

        public Action<bool> AddFiles;
        private DelegateCommand _addfilesCommand;
        public bool HasErrors { get; set; } = false;
        public ICommand AddFilesCommand
        {
            get
            {
                if (_addfilesCommand == null)
                {
                    _addfilesCommand = new DelegateCommand(_ =>
                    {
                        Validate(propertyName: null);

                        if (!HasErrors)
                        {
                            AddFiles(true);
                        }
                    });
                }

                return _addfilesCommand;
            }
        }
        private void Validate(string propertyName) {
            if (string.IsNullOrEmpty(this.ServiceClass) ||
                string.IsNullOrEmpty(this.IServiceClass) ||
                string.IsNullOrEmpty(this.ModelName) ||
                string.IsNullOrEmpty(this.DtoClass)
                )
            {
                this.HasErrors = true;
            }
            else {
                this.HasErrors = false;
            }
                
        }


    }
}

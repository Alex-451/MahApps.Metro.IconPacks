﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AsyncAwaitBestPractices;

namespace MahApps.Metro.IconPacks.Browser.ViewModels
{
    public class IconPackViewModel : ViewModelBase
    {
        private IEnumerable<IIconViewModel> _icons;
        private int _iconCount;
        private ICollectionView _iconsCollectionView;
        private string _filterText;
        private IIconViewModel _selectedIcon;
        private string _projectUrl;
        private string _licenseUrl;

        public IconPackViewModel(MainViewModel mainViewModel, string caption, Type enumType, Type packType)
        {
            this.MainViewModel = mainViewModel;
            this.Caption = caption;

            this.LoadEnumsAsync(enumType, packType).SafeFireAndForget();
        }

        private async Task LoadEnumsAsync(Type enumType, Type packType)
        {
            var collection = await Task.Run(() => GetIcons(enumType, packType).OrderBy(i => i.Name, StringComparer.InvariantCultureIgnoreCase).ToList());

            this.Icons = new ObservableCollection<IIconViewModel>(collection);
            this.IconCount = ((ICollection) this.Icons).Count;
            this.PrepareFiltering();
            this.SelectedIcon = this.Icons.First();
        }

        public IconPackViewModel(MainViewModel mainViewModel, string caption, Type[] enumTypes, Type[] packTypes)
        {
            this.MainViewModel = mainViewModel;
            this.Caption = caption;

            this.LoadAllEnumsAsync(enumTypes, packTypes).SafeFireAndForget();
        }

        private async Task LoadAllEnumsAsync(Type[] enumTypes, Type[] packTypes)
        {
            var collection = await Task.Run(() =>
            {
                var allIcons = Enumerable.Empty<IIconViewModel>();
                for (var counter = 0; counter < enumTypes.Length; counter++)
                {
                    allIcons = allIcons.Concat(GetIcons(enumTypes[counter], packTypes[counter]));
                }

                return allIcons.OrderBy(i => i.Name, StringComparer.InvariantCultureIgnoreCase).ToList();
            });

            this.Icons = new ObservableCollection<IIconViewModel>(collection);
            this.IconCount = ((ICollection) this.Icons).Count;
            this.PrepareFiltering();
            this.SelectedIcon = this.Icons.First();
        }

        private void PrepareFiltering()
        {
            this._iconsCollectionView = CollectionViewSource.GetDefaultView(this.Icons);
            this._iconsCollectionView.Filter = o => this.FilterIconsPredicate(this.FilterText, (IIconViewModel) o);
        }

        private bool FilterIconsPredicate(string filterText, IIconViewModel iconViewModel)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                return true;
            }
            else
            {
                var filterSubStrings = filterText.Split(new char[] {'+', ',', ';', '&'}, StringSplitOptions.RemoveEmptyEntries);

                foreach (var filterSubString in filterSubStrings)
                {
                    var filterOrSubStrings = filterSubString.Split(new char[] {'|'}, StringSplitOptions.RemoveEmptyEntries);

                    var isInName = filterOrSubStrings.Any(x => iconViewModel.Name.IndexOf(x.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0);
                    var isInDescription = filterOrSubStrings.Any(x => (iconViewModel.Description?.IndexOf(x.Trim(), StringComparison.CurrentCultureIgnoreCase) ?? -1) >= 0);

                    if (!(isInName || isInDescription)) return false;
                }

                return true;
            }
        }

        private static string GetDescription(Enum value)
        {
            var fieldInfo = value.GetType().GetField(value.ToString());
            return fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault() is DescriptionAttribute attribute ? attribute.Description : value.ToString();
        }

        private static IEnumerable<IIconViewModel> GetIcons(Type enumType, Type packType)
        {
            return Enum.GetValues(enumType)
                       .OfType<Enum>()
                       .Where(k => k.ToString() != "None")
                       .Select(k => GetIconViewModel(enumType, packType, k));
        }

        private static IIconViewModel GetIconViewModel(Type enumType, Type packType, Enum k)
        {
            var description = GetDescription(k);
            return new IconViewModel()
            {
                Name = k.ToString(),
                Description = description,
                IconPackType = packType,
                IconType = enumType,
                Value = k
            };
        }

        public MainViewModel MainViewModel { get; }

        public string Caption { get; }

        public IEnumerable<IIconViewModel> Icons
        {
            get { return _icons; }
            set { Set(ref _icons, value); }
        }

        public int IconCount
        {
            get { return _iconCount; }
            set { Set(ref _iconCount, value); }
        }

        public string ProjectUrl
        {
            get { return _projectUrl; }
            set { Set(ref _projectUrl, value); }
        }

        public string LicenseUrl
        {
            get { return _licenseUrl; }
            set { Set(ref _licenseUrl, value); }
        }

        public string FilterText
        {
            get { return _filterText; }
            set
            {
                if (Set(ref _filterText, value))
                {
                    this._iconsCollectionView?.Refresh();
                }
            }
        }

        public IIconViewModel SelectedIcon
        {
            get { return _selectedIcon; }
            set
            {
                if (Set(ref _selectedIcon, value) && !(_selectedIcon is null))
                {
                    var metaData = Attribute.GetCustomAttribute(_selectedIcon.IconPackType, typeof(MetaDataAttribute)) as MetaDataAttribute;
                    this.ProjectUrl = metaData != null ? metaData.ProjectUrl : string.Empty;
                    this.LicenseUrl = metaData != null ? metaData.LicenseUrl : string.Empty;
                }
            }
        }
    }

    public interface IIconViewModel
    {
        string Name { get; set; }
        string Description { get; set; }
        Type IconPackType { get; set; }
        Type IconType { get; set; }
        object Value { get; set; }
    }

    public class IconViewModel : ViewModelBase, IIconViewModel
    {
        public IconViewModel()
        {
            this.CopyToClipboard =
                new SimpleCommand
                {
                    CanExecuteDelegate = x => (x != null),
                    ExecuteDelegate = x => Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var icon = (IIconViewModel) x;
                        var text = $"<iconPacks:{icon.IconPackType.Name} Kind=\"{icon.Name}\" />";
                        Clipboard.SetDataObject(text);
                    }))
                };

            this.CopyToClipboardAsContent =
                new SimpleCommand
                {
                    CanExecuteDelegate = x => (x != null),
                    ExecuteDelegate = x => Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var icon = (IIconViewModel) x;
                        var text = $"{{iconPacks:{icon.IconPackType.Name.Replace("PackIcon", "")} Kind={icon.Name}}}";
                        Clipboard.SetDataObject(text);
                    }))
                };

            this.CopyToClipboardAsPathIcon =
                new SimpleCommand
                {
                    CanExecuteDelegate = x => (x != null),
                    ExecuteDelegate = x => Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var icon = (IIconViewModel) x;
                        // The UWP type is in WPF app not available
                        var text = $"<iconPacks:{icon.IconPackType.Name.Replace("PackIcon", "PathIcon")} Kind=\"{icon.Name}\" />";
                        Clipboard.SetDataObject(text);
                    }))
                };

            this.CopyToClipboardAsGeometry =
                new SimpleCommand
                {
                    CanExecuteDelegate = x => (x != null),
                    ExecuteDelegate = x => Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var icon = (IIconViewModel) x;
                        var iconPack = Activator.CreateInstance(icon.IconPackType) as PackIconControlBase;
                        if (iconPack == null) return;

                        var kindProperty = icon.IconPackType.GetProperty("Kind");
                        if (kindProperty == null) return;

                        kindProperty.SetValue(iconPack, icon.Value);

                        Clipboard.SetDataObject(iconPack.Data);
                    }))
                };
        }

        public ICommand CopyToClipboard { get; }

        public ICommand CopyToClipboardAsContent { get; }

        public ICommand CopyToClipboardAsPathIcon { get; }

        public ICommand CopyToClipboardAsGeometry { get; }

        public string Name { get; set; }

        public string Description { get; set; }

        public Type IconPackType { get; set; }

        public Type IconType { get; set; }

        public object Value { get; set; }
    }
}
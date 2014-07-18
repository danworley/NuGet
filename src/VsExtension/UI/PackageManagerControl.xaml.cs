﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using NuGet.VisualStudio;

namespace NuGet.Tools
{
    /// <summary>
    /// Interaction logic for PackageManagerControl.xaml
    /// </summary>
    public partial class PackageManagerControl : UserControl
    {
        private PackageManagerDocData _model;
        private bool _initialized;

        public PackageManagerDocData Model
        {
            get
            {
                return _model;
            }
        }

        public PackageManagerControl(PackageManagerDocData myDoc)
        {
            _model = myDoc;

            InitializeComponent();

            _packageDetail.Control = this;
            Update();
            _initialized = true;
        }

        private void Update()
        {
            _label.Content = string.Format(CultureInfo.CurrentCulture,
                "Package Manager: {0}",
                _model.Project.Name);

            // init source repo list
            _sourceRepoList.Items.Clear();
            var sources = _model.GetEnabledPackageSourcesWithAggregate();
            foreach (var source in sources)
            {
                _sourceRepoList.Items.Add(source);
            }
            _sourceRepoList.SelectedItem = _model.PackageSourceProvider.ActivePackageSource.Name;

            UpdatePackageList();
        }

        private void UpdatePackageList()
        {
            if (_model.Project != null)
            {
                SearchPackageInActivePackageSource();
            }
        }

        public void SetBusy(bool busy)
        {
            if (busy)
            {
                _busyControl.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                _busyControl.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void SetPackageListBusy(bool busy)
        {
            if (busy)
            {
                _listBusyControl.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                _listBusyControl.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private class PackageLoader : ILoader
        {
            private string _searchText;
            private IEnumerable<string> _supportedFrameworks;
            private IPackageRepository _repo;
            private IPackageRepository _localRepo;
            private const int pageSize = 15;

            public PackageLoader(
                IPackageRepository repo,
                IPackageRepository localRepo,
                string searchText,
                IEnumerable<string> supportedFrameworks)
            {
                _repo = repo;
                _localRepo = localRepo;
                _searchText = searchText;
                _supportedFrameworks = supportedFrameworks;
            }

            public Task<LoadResult> LoadItems(int startIndex, CancellationToken ct)
            {
                var query = _repo.Search(
                    searchTerm: _searchText,
                    targetFrameworks: _supportedFrameworks,
                    allowPrereleaseVersions: false).Skip(startIndex).Take(pageSize);

                return Task.Factory.StartNew<LoadResult>(() =>
                {
                    List<UiSearchResultPackage> packages = new List<UiSearchResultPackage>();
                    foreach (var p in query)
                    {
                        ct.ThrowIfCancellationRequested();

                        var searchResultPackage = new UiSearchResultPackage()
                        {
                            Id = p.Id,
                            Version = p.Version,
                            Summary = p.Summary,
                            IconUrl = p.IconUrl
                        };

                        var installedPackage = _localRepo.FindPackage(p.Id);
                        if (installedPackage != null)
                        {
                            if (installedPackage.Version < p.Version)
                            {
                                searchResultPackage.Status = PackageStatus.UpdateAvailable;
                            }
                            else
                            {
                                searchResultPackage.Status = PackageStatus.Installed;
                            }
                        }
                        else
                        {
                            searchResultPackage.Status = PackageStatus.NotInstalled;
                        }

                        searchResultPackage.AllVersions = GetAllVersions(_repo, searchResultPackage.Id);
                        packages.Add(searchResultPackage);
                    }

                    ct.ThrowIfCancellationRequested();
                    return new LoadResult()
                    {
                        Items = packages,
                        HasMoreItems = packages.Count == pageSize
                    };
                });
            }

            // Get all versions of the package
            private List<UiDetailedPackage> GetAllVersions(IPackageRepository repo, string id)
            {
                var allVersions = new List<UiDetailedPackage>();
                foreach (var p in repo.FindPackagesById(id))
                {
                    var detailedPackage = new UiDetailedPackage()
                    {
                        Id = p.Id,
                        Version = p.Version,
                        Summary = p.Summary,
                        Description = p.Description,
                        Authors = StringCollectionToString(p.Authors),
                        Owners = StringCollectionToString(p.Owners),
                        IconUrl = p.IconUrl,
                        LicenseUrl = p.LicenseUrl,
                        ProjectUrl = p.ProjectUrl,
                        Tags = p.Tags,
                        DownloadCount = p.DownloadCount,
                        Published = p.Published,
                        DependencySets = p.DependencySets,
                        NoDependencies = !HasDependencies(p.DependencySets),
                        Package = p
                    };

                    allVersions.Add(detailedPackage);
                }

                return allVersions;
            }

            private bool HasDependencies(IEnumerable<PackageDependencySet> dependencySets)
            {
                if (dependencySets == null)
                {
                    return false;
                }

                foreach (var dependencySet in dependencySets)
                {
                    if (dependencySet.Dependencies != null &&
                        dependencySet.Dependencies.Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            private string StringCollectionToString(IEnumerable<string> v)
            {
                if (v == null)
                {
                    return null;
                }

                string retValue = String.Join(", ", v);
                if (retValue == String.Empty)
                {
                    return null;
                }

                return retValue;
            }
        }

        private void SearchPackageInActivePackageSource()
        {
            string targetFramework = _model.Project.GetTargetFramework();
            var searchText = _searchText.Text;
            bool showOnlyInstalled = _filter.SelectedIndex == 1;

            if (showOnlyInstalled)
            {
                /* !!!
                _packageList.ItemsSource = null;
                _packageList.ItemsSource = _model.LocalRepo.GetPackages().ToList().Select
                    (p =>
                        {
                            return new PackageListItem(
                                p,
                                installed: true,
                                updateAvailable: false);
                        }); */
            }
            else
            {
                // search online
                var supportedFrameWorks = targetFramework != null ? new[] { targetFramework } : new string[0];
                var loader = new PackageLoader(
                    _model.ActiveSourceRepo,
                    _model.LocalRepo,
                    searchText,
                    supportedFrameWorks);
                _packageList.Loader = loader;
            }
        }

        private void SettingsButtonClick(object sender, RoutedEventArgs e)
        {
            var optionsPageActivator = ServiceLocator.GetInstance<IOptionsPageActivator>();
            optionsPageActivator.ActivatePage(
                OptionsPage.PackageSources,
                null);
        }

        private void PackageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedPackage = _packageList.SelectedItem as UiSearchResultPackage;
            if (selectedPackage == null)
            {
                _packageDetail.DataContext = null;
            }
            else
            {
                _packageDetail.DataContext = new PackageDetailControlModel(selectedPackage);
            }
        }

        private void _searchText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                SearchPackageInActivePackageSource();
            }
        }

        private void _sourceRepoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = _sourceRepoList.SelectedItem as string;
            if (string.IsNullOrEmpty(s))
            {
                return;
            }

            _model.ChangeActiveSourceRepo(s);
        }

        private void _filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initialized)
            {
                SearchPackageInActivePackageSource();
            }
        }

        internal void UpdatePackageStatus()
        {
            var installedPackages = new Dictionary<string, SemanticVersion>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in _model.LocalRepo.GetPackages())
            {
                installedPackages[package.Id] = package.Version;
            }

            foreach (var item in _packageList.ItemsSource)
            {
                var package = item as UiSearchResultPackage;
                if (package == null)
                {
                    continue;
                }

                SemanticVersion installedVersion;
                if (installedPackages.TryGetValue(package.Id, out installedVersion))
                {
                    if (installedVersion < package.Version)
                    {
                        package.Status = PackageStatus.UpdateAvailable;
                    }
                    else
                    {
                        package.Status = PackageStatus.Installed;
                    }
                }
                else
                {
                    package.Status = PackageStatus.NotInstalled;
                }
            }
        }
    }
}
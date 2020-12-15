using Newtonsoft.Json;
using Kanban4U.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Kanban4U
{
    public class GlobalSettings : INotifyPropertyChanged
    {
        private GlobalSettings() { }

        public static GlobalSettings Instance { get; private set; } = LoadSettings();
        private bool _loaded;

        public string SelectedNavigationViewItemTag
        {
            get
            {
                return _selectedNavigationViewItemTag;
            }
            set
            {
                if (this.SetProperty(PropertyChanged, ref _selectedNavigationViewItemTag, value))
                {
                    SaveSettings();
                }
            }
        }
        private string _selectedNavigationViewItemTag;

        public string PersonalAccessToken
        {
            get
            {
                return _personalAccessToken;
            }
            set
            {
                if (this.SetProperty(PropertyChanged, ref _personalAccessToken, value))
                {
                    SaveSettings();
                }
            }
        }
        private string _personalAccessToken;

        public bool EnableImpersonation
        {
            get
            {
                return _enableImpersonation;
            }
            set
            {
                if (this.SetProperty(PropertyChanged, ref _enableImpersonation, value))
                {
                    SaveSettings();
                }
            }
        }
        private bool _enableImpersonation;

        public VstsTeamMember UserToImpersonate
        {
            get
            {
                return _userToImpersonate;
            }
            set
            {
                if (this.SetProperty(PropertyChanged, ref _userToImpersonate, value))
                {
                    SaveSettings();
                }
            }
        }
        private VstsTeamMember _userToImpersonate;

        public string GetUserToImpersonateDisplayName()
        {
            return _enableImpersonation ? _userToImpersonate?.DisplayName : null;
        }


        public VstsTeam Team
        {
            get
            {
                return _team;
            }
            set
            {
                if (this.SetProperty(PropertyChanged, ref _team, value))
                {
                    UpdateTeamMembers();
                    SaveSettings();
                }
            }
        }
        private VstsTeam _team = new VstsTeam
        {
            TeamId = Configuration.CurrentConfig.TeamId,
            Name = Configuration.CurrentConfig.TeamName,
            Url = Configuration.CurrentConfig.TeamVSTSUri + "/_apis/projects" + Configuration.CurrentConfig.TeamProjectId + "/teams/" + Configuration.CurrentConfig.TeamId,
            IdentityUrl = "https://vssps.dev.azure.com/e/Microsoft/_apis/Identities/" + Configuration.CurrentConfig.TeamId,
            ProjectName = Configuration.CurrentConfig.TeamProjectName
        };

        private Dictionary<string, WorkItem> _cachedWorkItems = new Dictionary<string, WorkItem>();

        private object _cacheLock = new object();

        public bool TryGetCachedWorkItem(string id, out WorkItem item)
        {
            lock (_cacheLock)
            {
                return _cachedWorkItems.TryGetValue(id, out item);
            }
        }

        public void CacheWorkItem(WorkItem item)
        {
            lock (_cacheLock)
            {
                _cachedWorkItems[item.Id] = item;
            }
        }

        public void ClearWorkItemCache()
        {
            lock (_cacheLock)
            {
                _cachedWorkItems.Clear();
                SaveCachedWorkItems();
            }
        }

        public int GetCachedWorkItemsCount()
        {
            lock (_cacheLock)
            {
                return _cachedWorkItems.Count;
            }
        }

        [JsonIgnore]
        public ObservableCollection<VstsTeamMember> TeamMembers { get; private set; } = new ObservableCollection<VstsTeamMember>();

        [JsonIgnore]
        public ObservableCollection<VstsIteration> TeamIterations { get; private set; } = new ObservableCollection<VstsIteration>();

        private async void UpdateTeamMembers()
        {
            if (_loaded)
            {
                string teamId = _team?.TeamId;

                if (teamId != null)
                {
                    var list = await Logic.GetTeamMembers(_team, true);
                    if (teamId == _team.TeamId)
                    {
                        TeamMembers.Clear();
                        TeamMembers.Add(null);
                        list.ForEach(x => TeamMembers.Add(x));

                        var iterations = await Logic.GetTeamIterations(_team.TeamId);
                        if (teamId == _team.TeamId)
                        {
                            TeamIterations.Clear();
                            iterations.Values.OrderBy(x => x.StartDate).ToList().ForEach(x => TeamIterations.Add(x));
                        }
                    }
                }
            }
        }

        public static void OnLaunch()
        {
            Instance._loaded = true;

            Instance.UpdateTeamMembers();
        }

        private static GlobalSettings LoadSettings()
        {
            GlobalSettings instance = null;

            object settingsJsonObject;
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(c_globalSettingsKey, out settingsJsonObject))
            {
                string settingsJson = settingsJsonObject as string;

                try
                {
                    instance = JsonConvert.DeserializeObject<GlobalSettings>(settingsJson);
                }
                catch
                {

                }
            }

            if (instance == null)
            {
                instance = new GlobalSettings();
            }

            // Enable migration from old settings
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("PersonalAccessToken", out var personalAccessToken))
            {
                instance.PersonalAccessToken = personalAccessToken as string;
            }

            instance.LoadCachedWorkItems();

            return instance;
        }

        private void SaveSettings()
        {
            string settingsJson = JsonConvert.SerializeObject(this);

            ApplicationData.Current.LocalSettings.Values[c_globalSettingsKey] = settingsJson;
        }

        private const string c_globalSettingsKey = "GlobalSettings";

        private SemaphoreSlim _cacheFileLock = new SemaphoreSlim(1);
        private const string c_cacheFileName = "cachedworkitems.json";

        public async void SaveCachedWorkItems()
        {
            string workItemsJson;
            lock (_cacheLock)
            {
                workItemsJson = JsonConvert.SerializeObject(_cachedWorkItems, Formatting.Indented);
            }

            try
            {
                await _cacheFileLock.WaitAsync();
                var file = await OpenCacheFile();
                await FileIO.WriteTextAsync(file, workItemsJson);
            }
            finally
            {
                _cacheFileLock.Release();
            }
        }

        private async void LoadCachedWorkItems()
        {
            string workItemsJson;
            try
            {
                await _cacheFileLock.WaitAsync();
                var file = await OpenCacheFile();
                workItemsJson = await FileIO.ReadTextAsync(file);
            }
            finally
            {
                _cacheFileLock.Release();
            }

            lock (_cacheLock)
            {
                try
                {
                    _cachedWorkItems = JsonConvert.DeserializeObject<Dictionary<string, WorkItem>>(workItemsJson);
                }
                catch // If the format changes then just ignore the cache.
                {

                }
                if (_cachedWorkItems == null)
                {
                    _cachedWorkItems = new Dictionary<string, WorkItem>();
                }

                // WorkItem doesn't get serialized because it causes a self-reference, so fill that back in when deserializing.
                foreach (var item in _cachedWorkItems.Values)
                {
                    foreach (var burndown in item.TimeSpentOnDate)
                    {
                        burndown.WorkItem = item;
                    }
                }
            }
        }

        // This function helps open the cache file, retrying a few times in case virus scanner or something has the file open
        private async Task<StorageFile> OpenCacheFile(int retries = 5)
        {
            if (_cacheFile == null)
            {
                try
                {
                    _cacheFile = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(c_cacheFileName, CreationCollisionOption.OpenIfExists);
                }
                catch
                {
                }

                if (_cacheFile == null)
                {
                    if (retries > 0)
                    {
                        return await OpenCacheFile(retries - 1);
                    }
                }
            }
            return _cacheFile;
        }

        public string GitHubAccessToken
        {
            get
            {
                return _gitHubAccessToken;
            }
            set
            {
                if (this.SetProperty(PropertyChanged, ref _gitHubAccessToken, value))
                {
                    SaveSettings();
                }
            }
        }
        private string _gitHubAccessToken;

        private StorageFile _cacheFile;


        public event PropertyChangedEventHandler PropertyChanged;
    }

}

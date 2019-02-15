using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Windows.Security.Authentication.Web;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;
using Windows.Storage.Streams;
using Windows.Web.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using Windows.UI;
using Windows.UI.Xaml.Markup;

namespace Kanban4U
{
    public static class GitHubLogic
    {
        public static async Task<string> GetGitHubAccessToken()
        {
            var settings = GlobalSettings.Instance;
            if (settings.GitHubAccessToken == null)
            {
                settings.GitHubAccessToken = await GetGitHubAccessTokenWorker();
            }

            return settings.GitHubAccessToken;
        }

        private static SemaphoreSlim s_authenticationLock = new SemaphoreSlim(1);

        public static async Task<string> GetGitHubAccessTokenWorker()
        {
            await s_authenticationLock.WaitAsync();

            try
            {
                string accessCode = "";
                WebView webView = new WebView();
                ContentDialog dialog = new ContentDialog();
                //dialog.Content = "Do you want to authenticate to GitHub?";
                //dialog.PrimaryButtonText = "Yes";
                //dialog.PrimaryButtonClick += (sender, args) => { yesClicked = true; };
                //dialog.SecondaryButtonText = "No";
                dialog.Content = webView;

                webView.MinWidth = 400;
                webView.MinHeight = 700;

                //var msappUri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri();
                //System.Diagnostics.Debug.Assert(msappUri == new Uri("ms-app://s-1-15-2-3873301497-715253160-2862221639-955569770-3342954887-1966562334-537999965/"),
                //    "If app callback URI changes you need to update the Azure App Registration callback URIs as well");
                var redirectUri = new Uri("https://github.com/chrisglein/kanban4u");
                var redirectUriEscaped = Uri.EscapeDataString(redirectUri.AbsoluteUri);
                var state = DateTime.Now.Ticks.ToString(); // Unique string for this session
                var clientId = "4991bf58800983f36e15";

                var oauthUri = $"https://github.com/login/oauth/authorize?client_id={clientId}" +
                    $"&redirect_uri={redirectUriEscaped}" +
                    $"&scope=repo user" +
                    $"&state={state}";

                //var result = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.UseHttpPost | WebAuthenticationOptions.UseCorporateNetwork, new Uri(oauthUri));
                webView.Navigate(new Uri(oauthUri));

                webView.NavigationStarting += (sender, args) =>
                {
                    var uri = args.Uri;
                    var basePath = uri.GetLeftPart(UriPartial.Path);
                    if (new Uri(basePath) == redirectUri)
                    {
                        var payload = uri;
                        var query = HttpUtility.ParseQueryString(uri.Query);

                        accessCode = query.Get("code");

                    // Success, all done.
                    dialog.Hide();

                        webView.Stop();
                    }
                };

                await dialog.ShowAsync();

                if (!String.IsNullOrEmpty(accessCode))
                {
                    var client = new HttpClient();

                    var content = new HttpStringContent("", UnicodeEncoding.Utf8, "application/json");

                    var accessTokenUri = $"https://github.com/login/oauth/access_token?client_id={clientId}" +
                        $"&redirect_uri={redirectUriEscaped}" +
                        $"&client_secret=56b5262758944b332f6e1329c161cbf7ffe8f8e7" +
                        $"&code={accessCode}";

                    var response = await client.PostAsync(new Uri(accessTokenUri), content);

                    var result = await response.Content.ReadAsStringAsync();

                    var query = HttpUtility.ParseQueryString(result);
                    string token = query.Get("access_token");

                    return token;
                }

                return null;
            }
            finally
            {
                s_authenticationLock.Release();
            }
        }

        private static async Task<HttpClient> GetClient()
        {
            var accessToken = await GetGitHubAccessToken();

            // Authenticate using the personal access token
            var client = new HttpClient();

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new HttpMediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new HttpCredentialsHeaderValue("bearer", accessToken);
            client.DefaultRequestHeaders.Add("User-Agent", "KanBan4U");

            return client;
        }

        private static async Task<JObject> RunGraphQLQuery(string query)
        {
            using (var client = await GetClient())
            {
                var bodyDictionary = new Dictionary<string, string> { { "query", query } };
                var bodyJson = JsonConvert.SerializeObject(bodyDictionary);
                var content = new HttpStringContent(bodyJson, UnicodeEncoding.Utf8, "application/json");

                var response = await client.PostAsync(new Uri("https://api.github.com/graphql"), content);
                var result = await response.Content.ReadAsStringAsync();

                return JObject.Parse(result);
            }
        }


        public static async Task<GitHubPerson> GetCurrentUser()
        {
            var query = "query GetSelf { viewer { login } } ";

            var result = await RunGraphQLQuery(query);

            string login = result["data"]["viewer"]["login"].ToString();

            return await GetGitHubPerson(login);
        }

        public static async Task<List<GitHubIssue>> GetAssignedIssues(string login)
        {
            var query = @"
query GetAssignedIssues {
  search(query: ""assignee:"+login+ @" is:open"", type: ISSUE, first: 100) {
    edges {
        node {
            ... on Issue {
                url,
                title,
                labels(first:100) { edges { node { name, color } }}
                assignees(first:100) { edges { node { login } }}
                }
            }
        }
    }
}
";
            var result = await RunGraphQLQuery(query);

            var issues = new List<GitHubIssue>();
            foreach (var issueEdge in result["data"]["search"]["edges"])
            {
                var issue = issueEdge["node"].ToObject<GitHubIssue>();

                foreach (var labelEdge in issueEdge["node"]["labels"]["edges"])
                {
                    var label = labelEdge["node"].ToObject<GitHubLabel>();
                    issue.Labels.Add(label);
                }

                foreach (var assigneeEdge in issueEdge["node"]["assignees"]["edges"])
                {
                    var assignee = assigneeEdge["node"]["login"].ToString();
                    var person = await GetGitHubPerson(assignee);
                    issue.Assignees.Add(person);
                }

                issues.Add(issue);
            }

            return issues;
        }


        private static async Task EnsureGitHubToAADLinks()
        {
            lock (s_loginToPerson)
            {
                if (s_loginToPerson.Count > 0)
                {
                    return;
                }
            }

            var people = await Logic.GetGitHubToAADLinks();

            lock (s_loginToPerson)
            {
                foreach (var person in people)
                {
                    s_loginToPerson[person.GitHub.Login] = person;
                }
            }
        }

        private static async Task<GitHubPerson> GetGitHubPerson(string login)
        {
            await EnsureGitHubToAADLinks();

            lock (s_loginToPerson)
            {
                if (s_loginToPerson.TryGetValue(login, out var person))
                {
                    return person;
                }
            }

            return new GitHubPerson
            {
                GitHub = new GitHubPerson.GitHubIdentity
                {
                    Login = login
                }
            };
        }


        private static bool CheckHttpResponse(HttpResponseMessage response)
        {
            if (response.StatusCode != HttpStatusCode.Ok)
            {
                return true;
            }

            return false;
        }

        private static Dictionary<string, GitHubPerson> s_loginToPerson = new Dictionary<string, GitHubPerson>();
    }

    public class GitHubPerson
    {
        public GitHubIdentity GitHub { get; set; }
        public AadIdentity Aad { get; set; }

        public class GitHubIdentity
        {
            public string Id { get; set; }
            public string Login { get; set; }
            public List<string> Organizations { get; set; }
        }

        public class AadIdentity
        {
            public string Alias { get; set; }
            public string UserPrincipalName { get; set; }
            public string PreferredName { get; set; }
            public string Id { get; set; }
        }

        public override string ToString()
        {
            return GitHub.Login;
        }
    }

    public class GitHubIssue
    {
        public string Url { get; set; }
        public string Title { get; set; }
        [JsonIgnore]
        public List<GitHubPerson> Assignees { get; set; } = new List<GitHubPerson>();
        [JsonIgnore]
        public List<GitHubLabel> Labels { get; set; } = new List<GitHubLabel>();
    }

    public class GitHubLabel
    {
        public string Name { get; set; }
        public string Color { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}

using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

namespace Kanban4U.Vsts
{
    public static class VstsApiWrapper
    {
        #region Public API

        public static async Task<VstsQuery> GetQueryByPathAsync(string path)
        {
            return (await InvokeRestMethodAsync<VstsStoredQuery>(null, GetRetrieveQueryByPathRestApiUri(path), HttpMethod.Get)).FirstOrDefault();
        }

        public static async Task<List<VstsWorkItem>> RunQueryAsync(VstsQuery query)
        {
            var unpopulatedItems = await InvokeRestMethodAsync<VstsWorkItem>("workItems", query.GetRequestMessage());
            List<VstsWorkItem> workItems = new List<VstsWorkItem>();

            // Populate work item fields. VSTS only allows you to query for 200 items at a time,
            // so populate our item fields over several passes if necessary.
            const int maxWorkItemQueryCount = 200;
            int passesNeeded = (unpopulatedItems.Count / maxWorkItemQueryCount) + 1;

            for (int pass = 0; pass < passesNeeded; ++pass)
            {
                string workItemIds = "";

                for (int i = 0; i < Math.Min(unpopulatedItems.Count, maxWorkItemQueryCount); ++i)
                {
                    workItemIds += (workItemIds.Length > 0 ? "," : "") + unpopulatedItems[i].Id;
                }

                var populatedItems = await GetWorkItemsByIdsAsync(workItemIds);
                workItems.AddRange(populatedItems);
                unpopulatedItems.RemoveRange(0, populatedItems.Count);
            }

            return workItems;
        }

        public static void Fixup(VstsWorkItem workItem)
        {
            // The description is stored as HTML, so we want to strip the HTML tags and unescape everything.
            if (workItem.Fields.Description != null)
            {
                workItem.Fields.Description = System.Net.WebUtility.HtmlDecode(workItem.Fields.Description);
                string description = System.Text.RegularExpressions.Regex.Replace(workItem.Fields.Description, @"<.*?>", string.Empty);
                workItem.Fields.Description = description;
            }
        }

        public static async Task<List<VstsWorkItem>> GetWorkItemsByIdsAsync(string ids)
        {
            var workItems = await InvokeRestMethodAsync<VstsWorkItem>("value", GetRetrieveWorkItemsRestApiUri(ids), HttpMethod.Get);

            foreach (var workItem in workItems)
            {
                Fixup(workItem);
            }

            return workItems;
        }

        public static async Task<VstsWorkItem> GetWorkItemByIdAsync(string id)
        {
            return (await GetWorkItemsByIdsAsync(id)).FirstOrDefault();
        }

        public static async Task CommitUpdatesAsync(this VstsWorkItem workItem)
        {
            string jsonUpdateString = string.Empty;

            foreach (var workItemJsonObject in JObject.FromObject(workItem.Fields))
            {
                if (jsonUpdateString.Length > 0)
                {
                    jsonUpdateString += $",{Environment.NewLine}";
                }

                var workItemString = workItemJsonObject.Value.ToString().Replace(@"\", @"\\").Replace("\"", "\\\"");
                jsonUpdateString += $"    {{ \"op\": \"add\", \"path\": \"/fields/{workItemJsonObject.Key}\", \"value\": \"{workItemString}\" }}";
            }

            jsonUpdateString = $"[{Environment.NewLine}{jsonUpdateString}{Environment.NewLine}]";
            await InvokeRestMethodAsync<VstsWorkItem>(string.Empty, GetUpdateWorkItemRestApiUri(workItem.Id), HttpMethod.Patch, CreateHttpContentFromString(jsonUpdateString, HttpMethod.Patch));
        }

        #endregion

        #region REST API internals

        // These REST API URIs can be found documented at https://www.visualstudio.com/en-us/docs/integrate/api/wit/work-items.
        private const string _queryByPathRetrievalFormattingString = "{0}/DefaultCollection/{1}/_apis/wit/queries/{2}?api-version=2.0";
        private const string _workItemRetrievalFormattingString = "{0}/DefaultCollection/_apis/wit/WorkItems?ids={1}&fields={2}&api-version=2.0";
        private const string _workItemUpdateFormattingString = "{0}/DefaultCollection/_apis/wit/WorkItems/{1}?api-version=1.0";

        // These are IDs that refer to the VSTS API.
        private const string _resourceId = "499b84ac-1321-427f-aa17-267ca6975798";

        // I have no idea where these IDs were generated from, this is stolen from Scram2WP,
        // and... just works here, I wanted a unique one but again, no idea how it was generated.
        private const string _clientId = "872cd9fa-d31f-45e0-9eab-6e460a02d1f1";


        // These strings won't change, so we'll process them once and then cache the result.
        private static string _workItemFieldsSelectString = null;
        private static string _workItemFieldsToQueryString = null;

        static VstsApiWrapper()
        {
            string selectString = "";
            string fieldsToQuery = "";

            var fieldProperties = typeof(VstsWorkItemFields).GetProperties();
            foreach (var fieldProperty in fieldProperties)
            {
                var jsonAttribute = fieldProperty.GetCustomAttribute<JsonPropertyAttribute>(true);
                selectString += (jsonAttribute.PropertyName != null ? (selectString.Length > 0 ? ", [" : " [") + jsonAttribute.PropertyName + "]" : "");
                fieldsToQuery += (jsonAttribute.PropertyName != null ? (fieldsToQuery.Length > 0 ? "," : "") + jsonAttribute.PropertyName : "");
            }

            _workItemFieldsSelectString = selectString;
            _workItemFieldsToQueryString = fieldsToQuery;
        }

        private static string GetRetrieveQueryByPathRestApiUri(string path)
        {
            return string.Format(_queryByPathRetrievalFormattingString, Configuration.CurrentConfig.TeamVSTSUri, Configuration.CurrentConfig.TeamProjectName, path);
        }

        private static string GetRetrieveWorkItemsRestApiUri(string workItemIds)
        {
            return string.Format(_workItemRetrievalFormattingString, Configuration.CurrentConfig.TeamVSTSUri, workItemIds, _workItemFieldsToQueryString);
        }

        private static string GetUpdateWorkItemRestApiUri(string workItemId)
        {
            return string.Format(_workItemUpdateFormattingString, Configuration.CurrentConfig.TeamVSTSUri, workItemId);
        }

        public static IHttpContent CreateHttpContentFromString(string jsonString, HttpMethod method)
        {
            var content = new HttpStringContent(jsonString);
            content.Headers.ContentType = HttpMediaTypeHeaderValue.Parse(method == HttpMethod.Patch ? "application/json-patch+json" : "application/json");
            return content;
        }

        private static async Task<List<T>> InvokeRestMethodAsync<T>(string idToRetrieve, string requestUri, HttpMethod method, IHttpContent content = null)
        {
            return await InvokeRestMethodAsync<T>(idToRetrieve, new HttpRequestMessage(method, new Uri(requestUri)) { Content = content });
        }

        private static async Task<List<T>> InvokeRestMethodAsync<T>(string idToRetrieve, HttpRequestMessage requestMessage)
        {
            List<T> responseList = new List<T>();

            using (var client = await CreateHttpClientAsync())
            {
                using (var request = await client.SendRequestAsync(requestMessage).AsTask()) //  I think we crash if this timeouts?
                {
                    request.EnsureSuccessStatusCode();

                    var responseJObj = JObject.Parse(await request.Content.ReadAsStringAsync());

                    if (!string.IsNullOrWhiteSpace(idToRetrieve))
                    {
                        var responseJTokens = responseJObj[idToRetrieve].Children().ToList();

                        foreach (var token in responseJTokens)
                        {
                            responseList.Add(token.ToObject<T>());
                        }
                    }
                    else
                    {
                        responseList.Add(responseJObj.ToObject<T>());
                    }
                }
            }

            return responseList;
        }

        #endregion

        #region Connection and authentication

        private static AuthenticationContext GetAuthenticationContext(string tenant = null)
        {
            AuthenticationContext ctx = null;

            if (tenant != null)
            {
                ctx = new AuthenticationContext("https://login.microsoftonline.com/" + tenant);
            }
            else
            {
                ctx = new AuthenticationContext("https://login.windows.net/common");
                if (ctx.TokenCache.Count > 0)
                {
                    string homeTenant = ctx.TokenCache.ReadItems().First().TenantId;
                    ctx = new AuthenticationContext("https://login.microsoftonline.com/" + homeTenant);
                }
                else
                {
                    ctx = new AuthenticationContext("https://login.microsoftonline.com/microsoft.onmicrosoft.com");
                }
            }

            return ctx;
        }

        private static async Task<AuthenticationResult> GetAuthenticationResult(bool retry = false)
        {
            var authCtx = GetAuthenticationContext();
            try
            {
                var platformParameters = new PlatformParameters(PromptBehavior.Auto, true);
                var authResult = await authCtx.AcquireTokenAsync(_resourceId, _clientId, new Uri("urn:ietf:wg:oauth:2.0:oob"), platformParameters);

                System.Diagnostics.Debug.WriteLine("Token expires on: " + authResult.ExpiresOn);

                return authResult;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw ex;
            }
            catch (AdalServiceException ex)
            {
                if (ex.ErrorCode == "authentication_canceled" && retry)
                {
                    authCtx.TokenCache.Clear();
                    // Try again
                    return await GetAuthenticationResult(false);
                }
                else
                {
                    throw ex;
                }
            }
        }

        public static async Task<HttpClient> CreateHttpClientAsync()
        {
            AuthenticationResult authResult = await GetAuthenticationResult();

            var client = new HttpClient();

            client.DefaultRequestHeaders.Accept.Add(new Windows.Web.Http.Headers.HttpMediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-TFS-FedAuthRedirect", "Supress");
            client.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Bearer", authResult.AccessToken);

            return client;
        }


        #endregion
    }
}

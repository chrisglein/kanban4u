using System;
using Windows.Web.Http;

namespace Kanban4U.Vsts
{
    public class VstsCustomQuery : VstsQuery
    {
        public VstsCustomQuery(string queryString)
        {
            QueryString = queryString;
        }

        public string QueryString { get; private set; }

        public override HttpRequestMessage GetRequestMessage()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Configuration.CurrentConfig.TeamVSTSUri + "/DefaultCollection/OS/_apis/wit/wiql?api-version=2.0"));

            string content = "{ \"query\": \"SELECT [System.Id] FROM WorkItems WHERE " + QueryString + "\" }";
            request.Content = new HttpStringContent(content, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");

            return request;
        }
    }
}

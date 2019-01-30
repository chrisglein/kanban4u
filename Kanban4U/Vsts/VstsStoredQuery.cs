using System;
using Windows.Web.Http;

namespace Kanban4U.Vsts
{
    public class VstsStoredQuery : VstsQuery
    {
        public VstsStoredQuery(string id)
        {
            Id = id;
        }

        public string Id { get; private set; }

        public override HttpRequestMessage GetRequestMessage()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(string.Format(Configuration.CurrentConfig.TeamVSTSUri + "/DefaultCollection/OS/_apis/wit/wiql/{0}?api-version=2.0", Id)));
            return request;
        }
    }
}

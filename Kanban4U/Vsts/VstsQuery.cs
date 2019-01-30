using Windows.Web.Http;

namespace Kanban4U.Vsts
{
    public abstract class VstsQuery
    {
        public abstract HttpRequestMessage GetRequestMessage();
    }
}

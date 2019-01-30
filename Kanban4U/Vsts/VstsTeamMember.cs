using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban4U
{
    public class VstsTeamMember
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string UniqueName { get; set; }
        public string Url { get; set; }
        public bool IsContainer { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is VstsTeamMember rhs)
            {
                return rhs.ToString() == ToString();
            }
            return false;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return $"{DisplayName} <{UniqueName}>";
        }
    }
}

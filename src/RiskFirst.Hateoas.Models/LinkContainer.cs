using System.Xml.Serialization;
using System.Text.Json.Serialization;

namespace RiskFirst.Hateoas.Models
{
    public abstract class LinkContainer : ILinkContainer
    {
        [XmlElement("link")]
        [JsonPropertyName("_links")]
        public LinkCollection Links { get; set; } = new LinkCollection();

        public void Add(Link link)
        {
            Links.Add(link);
        }
    }
}

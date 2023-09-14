using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace podloader
{
    [XmlRoot(ElementName = "enclosure")]
    public class Enclosure
    {
        [XmlAttribute(AttributeName = "url")]
        public string Url { get; set; }

        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        [XmlAttribute(AttributeName = "length")]
        public long Length { get; set; }
    }

    [XmlRoot(ElementName = "itunes:image", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
    public class iTunesImage
    {
        [XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }
    }

    [XmlRoot(ElementName = "itunes:owner", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
    public class iTunesOwner
    {
        [XmlElement(ElementName = "itunes:name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "itunes:email")]
        public string Email { get; set; }
    }

    [XmlRoot(ElementName = "itunes:block", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
    public class iTunesBlock
    {
        [XmlText]
        public string Value { get; set; }
    }

    [XmlRoot(ElementName = "item")]
    public class Item
    {
        [XmlElement(ElementName = "title")]
        public string Title { get; set; }

        [XmlElement(ElementName = "description")]
        public string Description { get; set; }

        [XmlElement(ElementName = "pubDate")]
        public string PubDate { get; set; }

        [XmlElement(ElementName = "enclosure")]
        public Enclosure Enclosure { get; set; }

        [XmlElement(ElementName = "guid")]
        public Guid Guid { get; set; }

        [XmlElement(ElementName = "itunes:author", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public string iTunesAuthor { get; set; }

        [XmlElement(ElementName = "itunes:subtitle", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public string iTunesSubtitle { get; set; }

        [XmlElement(ElementName = "itunes:summary", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public string iTunesSummary { get; set; }

        [XmlElement(ElementName = "itunes:explicit", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public string iTunesExplicit { get; set; }

        [XmlElement(ElementName = "itunes:duration", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public string iTunesDuration { get; set; }
    }

    [XmlRoot(ElementName = "channel")]
    public class Channel
    {
        [XmlElement(ElementName = "title")]
        public string Title { get; set; }

        [XmlElement(ElementName = "link")]
        public string Link { get; set; }

        [XmlElement(ElementName = "description")]
        public string Description { get; set; }

        [XmlElement(ElementName = "language")]
        public string Language { get; set; }

        [XmlElement(ElementName = "pubDate")]
        public string PubDate { get; set; }

        [XmlElement(ElementName = "lastBuildDate")]
        public string LastBuildDate { get; set; }

        [XmlElement(ElementName = "itunes:author", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public string iTunesAuthor { get; set; }

        [XmlElement(ElementName = "itunes:keywords", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public string iTunesKeywords { get; set; }

        [XmlElement(ElementName = "itunes:explicit", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public string iTunesExplicit { get; set; }

        [XmlElement(ElementName = "itunes:image", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public iTunesImage iTunesImage { get; set; }

        [XmlElement(ElementName = "itunes:owner", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public iTunesOwner iTunesOwner { get; set; }

        [XmlElement(ElementName = "itunes:block", Namespace = "http://www.itunes.com/dtds/podcast-1.0.dtd")]
        public iTunesBlock iTunesBlock { get; set; }

        [XmlElement(ElementName = "item")]
        public List<Item> Item { get; set; }
    }

    [XmlRoot(ElementName = "rss")]
    public class Rss
    {
        [XmlAttribute(AttributeName = "version")]
        public string Version { get; set; }

        [XmlAttribute(AttributeName = "itunes", Namespace = "http://www.w3.org/2000/xmlns/")]
        public string iTunesNamespace = "http://www.itunes.com/dtds/podcast-1.0.dtd";

        [XmlAttribute(AttributeName = "media", Namespace = "http://www.w3.org/2000/xmlns/")]
        public string MediaNamespace = "http://search.yahoo.com/mrss/";

        [XmlElement(ElementName = "channel")]
        public Channel Channel { get; set; }
    }
}

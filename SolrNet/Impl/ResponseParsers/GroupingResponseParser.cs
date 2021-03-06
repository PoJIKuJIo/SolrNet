﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using SolrNet.Utils;

namespace SolrNet.Impl.ResponseParsers {
    /// <summary>
    /// Parses group.fields from query response
    /// </summary>
    /// <typeparam name="T">Document type</typeparam>
    public class GroupingResponseParser<T> : ISolrResponseParser<T> {
        private readonly ISolrDocumentResponseParser<T> docParser;

        public void Parse(XDocument xml, AbstractSolrQueryResults<T> results) {
            results.Switch(query: r => Parse(xml, r),
                           moreLikeThis: F.DoNothing);
        }

        public GroupingResponseParser(ISolrDocumentResponseParser<T> docParser) {
            this.docParser = docParser;
        }

        /// <summary>
        /// Parses the grouped elements
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="results"></param>
        public void Parse(XDocument xml, SolrQueryResults<T> results) {
            var mainGroupingNode = xml.XPathSelectElement("response/lst[@name='grouped']");
            if (mainGroupingNode == null)
                return;

            var groupings =
                from groupNode in mainGroupingNode.Elements()
                let groupName = groupNode.Attribute("name").Value
                let groupResults = ParseGroupedResults(groupNode)
                select new {groupName, groupResults};

            results.Grouping = groupings.ToDictionary(x => x.groupName, x => x.groupResults);
        }

        /// <summary>
        /// Parses collapsed document.ids and their counts
        /// </summary>
        /// <param name="groupNode"></param>
        /// <returns></returns>
        public GroupedResults<T> ParseGroupedResults(XElement groupNode) {

            var ngroupNode = groupNode.XPathSelectElement("int[@name='ngroups']");

            return new GroupedResults<T> {
                Groups = ParseGroup(groupNode).ToList(),
                Matches = Convert.ToInt32(groupNode.XPathSelectElement("int[@name='matches']").Value),
                Ngroups = ngroupNode == null ? null : (int?)int.Parse(ngroupNode.Value),
            };
        }

        /// <summary>
        /// Parses collapsed document.ids and their counts
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public IEnumerable<Group<T>> ParseGroup(XElement node) {
            return
                from docNode in node.XPathSelectElement("arr[@name='groups']").Elements()
                let groupValueNode = docNode.XPathSelectElements("*[@name='groupValue']").FirstOrDefault()
                where groupValueNode != null
                let groupValue = groupValueNode.Name == "null"
                                     ? "UNMATCHED"
                                     : //These are the results that do not match the grouping
                                 groupValueNode.Value
                let resultNode = docNode.XPathSelectElement("result[@name='doclist']")
                let numFound = Convert.ToInt32(resultNode.Attribute("numFound").Value)
                let docs = docParser.ParseResults(resultNode).ToList()
                select new Group<T> {
                    GroupValue = groupValue,
                    Documents = docs,
                    NumFound = numFound,
                };
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.UI.WebControls;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data.Items;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.ContentSearch.Linq.Utilities;
using Sitecore.Search;
using Sitecore.SharedSource.Data.Comparers.ItemComparers;


namespace Verndale.SharedSource.Helpers
{
    /// <summary>
    /// The SearchHelper class contains methods for searching for items in the Sitecore database
    /// using the ADC searchContrib module.
    /// </summary>
    public class SearchHelper
    {
        public static string FormatKeywordWithSpacesForSearch(string searchString)
        {
            var formattedSearchString = searchString.Contains(" ") &&
                                       (!searchString.StartsWith("\"") && !searchString.EndsWith("\""))
                ? "\"" + searchString + "\""
                : searchString;

            return formattedSearchString;
        }

        /// <summary>
        /// Searches for items.
        /// </summary>
        public static List<Item> GetItems
        (
            string indexName, 
            string language, 
            string templateGuidFilter, 
            string locationGuidFilter, 
            string fullTextQuery = "",
            Dictionary<string, string> refinementFilter = null,
            bool sortAfterExecution = false,
            string sortFieldName = "",
            bool sortAscending = false,
            int? pageNum = null,
            int? pageSize = null
        )
        {
            var resultItems = new List<Item>();

            using (var context = ContentSearchManager.GetIndex(indexName).CreateSearchContext())
            {
                // use the predicate builder to build out criteria for location guid, appended with 'OR'
                var locationSearch = PredicateBuilder.True<SearchResultItem>();
                foreach (var locationGuid in locationGuidFilter.Split('|'))
                {
                    var locationItem = SitecoreHelper.ItemMethods.GetItemFromGUID(locationGuid);
                    locationSearch = locationSearch.Or(t => t.Path.StartsWith(locationItem.Paths.Path));
                }

                // use the predicate builder to build out criteria for template guid, appended with 'OR'
                var templateSearch = PredicateBuilder.True<SearchResultItem>();
                foreach (var templateGuid in templateGuidFilter.Split('|'))
                {
                    var newTemplateGuid = Sitecore.Data.ID.Parse(templateGuid);
                    templateSearch = templateSearch.Or(t => t.TemplateId == newTemplateGuid);
                }

                // use the predicate builder to build out search terms, searching on each word in phrase separately, appended by AND
                // TODO: should it be 'OR'?
                // TODO: what if user enters in quotes for exact phrase search, this scenario should be taken into consideration
                var termSearch = PredicateBuilder.True<SearchResultItem>();
                if (!string.IsNullOrEmpty(fullTextQuery))
                {
                    if (fullTextQuery.Contains("\""))
                    {
                        termSearch = termSearch.And(t => t.Content.Contains(fullTextQuery));
                    }
                    else
                    {
                        foreach (var term in fullTextQuery.Split(' '))
                        {
                            var newTerm = term;
                            termSearch = termSearch.And(t => t.Content.Contains(newTerm));

                            // TODO: Implement a way to leverage wildcard
                            // something like below should be used with wildcard (*, ?) searches
                            // termSearch = termSearch.And(r => r["headline_t"].MatchWildcard(keyword));
                        }
                    }
                }

                // TODO: Add in DateRange filters

                // TODO: Add in Refinements, a dictionary of Fieldname/Value that should be added as criteria
                // also allow to specify whether these are appended together with an OR or an AND
                var refinementSearch = PredicateBuilder.True<SearchResultItem>();
                if (refinementFilter != null)
                {
                    foreach (var refinement in refinementFilter)
                    {
                        var fieldName = refinement.Key.ToLowerInvariant();
                        var fieldValue = IdHelper.ProcessGUIDs(refinement.Value);
                        refinementSearch = refinementSearch.And(t => t[fieldName].Contains(fieldValue));
                    }
                }

                // TODO: Add in boosting
                
                
                // start building out the query, specifying the language
                var query = context.GetQueryable<SearchResultItem>().Where(x => x.Language == language);

                // if locationguid was set, add in the location query
                if (!string.IsNullOrEmpty(locationGuidFilter))
                    query = query.Where(locationSearch);

                // if templateguid was set, add in the template query
                if (!string.IsNullOrEmpty(templateGuidFilter))
                    query = query.Where(templateSearch);

                // if fulltextquery was set, add in the termSearch query
                if (!string.IsNullOrEmpty(fullTextQuery))
                    query = query.Where(termSearch);

                // there are two ways to sort, 1: upon execution of the query, 2: after results are returned
                // if searching upon execution, you can specify the order by and build the pagination directly into the query
                // you may get unexpected results though! Because of how the index has tokenized the fields and words
                // index queries are very fast and there shouldn't be a problem sorting AFTER the results are returned
                // You can then use a FieldValueComparer which should take the data type into consideration (eg when sorting by date and numbers)
                if (sortAfterExecution)
                {
                    // execute the search and cast into a list
                    resultItems = query.Select(toItem => toItem.GetItem()).ToList();

                    // if sorting is set sort by field value comparer and use 'reverse' for descending
                    if (!string.IsNullOrEmpty(sortFieldName))
                    {

                        FieldValueComparer comparer = new FieldValueComparer(sortFieldName);
                        resultItems.Sort(comparer);
                        if (!sortAscending)
                            resultItems.Reverse();
                    }

                    // apply paging with Skip/Take
                    if (pageSize.HasValue)
                    {
                        // Default pageNum to 1 if no value
                        if (!pageNum.HasValue)
                            pageNum = 1;

                        // Calculate the starting record
                        var startRowIndex = (pageNum.Value - 1) * pageSize.Value;

                        resultItems = resultItems.Skip(startRowIndex).Take(pageSize.Value).ToList();
                    }
                }
                else
                {
                    // if attempting to sort during query execution, set with OrderBy or OrderByDescending
                    if (!string.IsNullOrEmpty(sortFieldName))
                    {
                        if (sortAscending)
                            query = query.OrderBy(x => x[sortFieldName]);
                        else
                            query = query.OrderByDescending(x => x[sortFieldName]);
                    }

                    // if pageSize is set, use built in .Page method, otherwise, can just select into an Item list
                    if (pageSize.HasValue)
                    {
                        // Default pageNum to 1 if no value
                        if (!pageNum.HasValue)
                            pageNum = 1;
                        resultItems = query.Select(toItem => toItem.GetItem()).Page(pageNum.Value, pageSize.Value).ToList();
                    }
                    else
                        resultItems = query.Select(toItem => toItem.GetItem()).ToList();
                    
                }
            }

            return resultItems;
        }

        public static void GetFacets()
        {
            using (var context = ContentSearchManager.GetIndex("sitecore_master_index").CreateSearchContext())
            {
                //TODO: build out faceting

                //var results = context.GetQueryable<SearchResultItem>().Where(prod => prod.Content.Contains("search box text").FacetOn(f => f.Color).FacetOn(f => f.Gender)).GetFacets();
                //For every facet that you want, just continue to use the FacetOn()

                //int totalHits = results.TotalHits;
                //var searchResuls = results.Hits;
                //var facets = results.Facets;
            }

        }
    }
}

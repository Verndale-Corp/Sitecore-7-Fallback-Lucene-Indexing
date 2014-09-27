using System.Linq;
using Lucene.Net.Documents;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Boosting;
using Sitecore.ContentSearch.ComputedFields;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.Diagnostics;
using System;
using System.Collections;
using System.Collections.Concurrent;
using Sitecore.ContentSearch.LuceneProvider;
using Sitecore.Data.Items;
using Sitecore.Data.Fields;
using Sitecore.SharedSource.PartialLanguageFallback.Extensions;
using Sitecore.SharedSource.PartialLanguageFallback.Managers;

namespace Verndale.SharedSource.SitecoreProviders
{
    public class CustomLuceneDocumentBuilder : LuceneDocumentBuilder
    {
        private readonly LuceneSearchFieldConfiguration defaultTextField = new LuceneSearchFieldConfiguration("NO", "TOKENIZED", "NO", 1f);

        public CustomLuceneDocumentBuilder(IIndexable indexable, IProviderUpdateContext context)
          : base(indexable, context)
        {
        }

        public override void AddField(IIndexableDataField field)
        {
            object fieldValue = this.Index.Configuration.FieldReaders.GetFieldValue(field);

            //UPDATED, Added By Verndale for Fallback
            //<!--ADDED FOR FALLBACK DEMO-->
            if (fieldValue == null || fieldValue == "")
            {
                // Get the Sitecore field for the Indexable Data Field (which is more generic) that was passed in
                // If the field is valid for fallback, then use the ReadFallbackValue method to try and get a value
                Sitecore.Data.Fields.Field thisField = (Sitecore.Data.Fields.Field)(field as SitecoreItemDataField);
                if (thisField.ValidForFallback())
                {
                    // ReadFallbackValue will get the fallback item for the current item 
                    // and will try to get the field value for it using fallbackItem[field.ID]
                    // Merely calling fallbackItem[field.ID] triggers the GetStandardValue method
                    // which has been overridden in the standard values provider override FallbackLanguageProvider
                    // which will in turn call ReadFallbackValue recursively until it finds a value or reaches a language that doesn't fallback 
                    fieldValue = FallbackLanguageManager.ReadFallbackValue(thisField, thisField.Item);
                }
            }

            string name = field.Name;
            LuceneSearchFieldConfiguration fieldSettings = this.Index.Configuration.FieldMap.GetFieldConfiguration(field) as LuceneSearchFieldConfiguration;
            if (fieldSettings == null || fieldValue == null)
                return;
            float boost = BoostingManager.ResolveFieldBoosting(field);
            if (IndexOperationsHelper.IsTextField(field))
            {
                LuceneSearchFieldConfiguration fieldConfiguration = this.Index.Configuration.FieldMap.GetFieldConfiguration("_content") as LuceneSearchFieldConfiguration;
                this.AddField("_content", fieldValue, fieldConfiguration ?? this.defaultTextField, 0.0f);
            }
            this.AddField(name, fieldValue, fieldSettings, boost);
        }
    }
}

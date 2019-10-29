using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DataMapping;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.HTTP;

namespace Couchbase.Search
{
    /// <summary>
    /// A client for making FTS <see cref="IFtsQuery"/> requests and mapping the responses to <see cref="ISearchResult"/>'s.
    /// </summary>
    /// <seealso cref="ISearchClient" />
    internal class SearchClient : HttpServiceBase, ISearchClient
    {
        //private static readonly ILog Log = LogManager.GetLogger<SearchClient>();

        //for log redaction
        //private Func<object, string> User = RedactableArgument.UserAction;

        public SearchClient(ClusterContext context) : this(
            new HttpClient(new AuthenticatingHttpClientHandler(context)),
            new SearchDataMapper(), context)
        {
        }

        public SearchClient(HttpClient httpClient, IDataMapper dataMapper, ClusterContext context)
            : base(httpClient, dataMapper, context)
        { }

        /// <summary>
        /// Executes a <see cref="IFtsQuery" /> request including any <see cref="ISearchOptions" /> parameters.
        /// </summary>
        /// <param name="searchQuery"></param>
        /// <returns></returns>
        public ISearchResult Query(SearchQuery searchQuery)
        {
            return QueryAsync(searchQuery)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Executes a <see cref="IFtsQuery" /> request including any <see cref="ISearchOptions" /> parameters asynchronously.
        /// </summary>
        /// <returns>A <see cref="ISearchResult"/> wrapped in a <see cref="Task"/> for awaiting on.</returns>
        public async Task<ISearchResult> QueryAsync(SearchQuery searchQuery, CancellationToken cancellationToken = default)
        {
            // try get Search node
            var node = Context.GetRandomNodeForService(ServiceType.Search);
            var uriBuilder = new UriBuilder(node.SearchUri)
            {
                Path = $"api/index/{searchQuery.Index}/query"
            };

            var searchResult = new SearchResult();
            var searchBody = searchQuery.ToJson();

            try
            {
                using var content = new StringContent(searchBody, Encoding.UTF8, MediaType.Json);
                var response = await HttpClient.PostAsync(uriBuilder.Uri, content, cancellationToken).ConfigureAwait(false);
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        searchResult = await DataMapper.MapAsync<SearchResult>(stream, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        using var reader = new StreamReader(stream);
                        var errorResult = await reader.ReadToEndAsync().ConfigureAwait(false);
                    }
                }

                searchResult.HttpStatusCode = response.StatusCode;
                if (searchResult.ShouldRetry())
                {
                    UpdateLastActivity();
                    return searchResult;
                }
            }
            catch (OperationCanceledException e)
            {
                throw new AmbiguousTimeoutException("The query was timed out via the Token.", e);
            }
            catch (HttpRequestException e)
            {
                throw new RequestCanceledException("The query was canceled.", e);
            }
            UpdateLastActivity();
            return searchResult;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion

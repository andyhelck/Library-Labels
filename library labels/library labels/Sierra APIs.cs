using RestSharp;
using System;
using System.Diagnostics;
using System.Text;


namespace Library_Labels_Namespace
{

    public class AuthToken
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }

    }

    public enum Branch
    {
        patrons,
        items,
        orders,
        bibs
    }

    public sealed class SimpleClient
    {
        private string _baseUrl = null;
        private string _clientKey = null;
        private string _clientSecret = null;

        private RestClient _client { get; set; }
        private RestRequest _request { get; set; }
        private IRestResponse _restResponse { get; set; }
        private AuthToken _authToken { get; set; }
        private string _accessToken { get; set; }

        public bool HasClient { get { return _client != null;} } // fails with bogus url
        public bool HasRequest { get { return _request != null; } } // would fail with bad credentials
        public bool HasAccess { get { return _accessToken != null; }  }
        public bool HasItemPermissions = false;
        public bool HasBibPermissions = false;


        // The access token must not have Bearer before the token

        // To retrieve an Access Token
        // base url example = "https://{your library}/iii/sierra-api/v5/";
        public SimpleClient(string baseUrl, string clientKey, string clientSecret, bool checkPermissions)
        {
            _baseUrl = baseUrl;
            _clientKey = clientKey;
            _clientSecret = clientSecret;
            _client = null;
            _request = null;
            _restResponse = null;
            _accessToken = null;

            try
            {
                _client = new RestClient(_baseUrl);
            }
            catch (Exception e)
            {
                Log.AppendError($"SimpleClient Error trying to create _client: {e.ToString()}");
                _client = null;
            }
            if (_client == null) return;


            var stringToEncode = Encoding.UTF8.GetBytes(_clientKey + ":" + _clientSecret);
            var headerValue = "Basic " + Convert.ToBase64String(stringToEncode);
            try
            {
                _request = new RestRequest("token", Method.POST);
            }
            catch (Exception e)
            {
                Log.AppendError($"SimpleClient Error trying to create _request: {e.ToString()}");
                _request = null;
            }
            if (_request == null) return;

            _request.AddHeader("Authorization", headerValue);

            try
            {
                _authToken = _client.Execute<AuthToken>(_request).Data;
                _accessToken = _authToken.access_token;
            }
            catch (Exception e)
            {
                Log.AppendError($"SimpleClient Error trying to Execute AuthToken request: {e.ToString()}");
                _authToken = null;
                _accessToken = null;
            }
            if (_authToken == null) return;

            if (checkPermissions)
            {
                try
                {
                    _request = CreateRestRequest(Branch.items, $"/?limit=1&offset=0", Method.GET);
                    _restResponse = Execute(_request);
                    HasItemPermissions = true;

                }
                catch (Exception e)
                {
                    Log.AppendError($"SimpleClient Error testing Item Permisions: {e.ToString()}");
                    HasItemPermissions = false;
                }

                try
                {
                    _request = CreateRestRequest(Branch.bibs, $"/?limit=1&offset=0", Method.GET);
                    _restResponse = Execute(_request);
                    HasBibPermissions = true;

                }
                catch (Exception e)
                {
                    Log.AppendError($"SimpleClient Error testing Bib Permisions: {e.ToString()}");
                    HasBibPermissions = false;
                }
                _request = CreateRestRequest(Branch.bibs, $"/?limit=1&offset=0", Method.GET);
                _restResponse = Execute(_request);

            }





            try
            {
                Log.AppendRaw($"SimpleClient constructor \n{_baseUrl}\n{_clientKey}\n{_clientSecret}\n{_accessToken}\n{_authToken.expires_in}");
            }
            catch (Exception e)
            {
                Log.AppendError($"Simple Client error {e.ToString()}");
            }

        }



        // Branch.patrons, "/find", Method.GET
        public RestRequest CreateRestRequest(Branch branch, string resource, Method method) // this generates the request, I think it needs to be called somethintg else poop
        {
            if (_accessToken == null)
            {
                Log.AppendError("CreateRestRequest Error: _accessToken is null");
                return null;
            }
            if (!resource.StartsWith("/")) resource = "/" + resource;
            var request = new RestRequest(branch + resource, method);
            request.AddHeader("Authorization", "bearer " + _accessToken);
            return request;
        }

        internal IRestResponse Execute(RestRequest request)
        {
            return _client.Execute(request);
        }
    }




}

using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Oauth2;
using Google.Apis.Oauth2.v2;
using Google.Apis.Oauth2.v2.Data;
using Google.Apis.Services;
using Google.Apis.Util;
using Google.Apis.Drive.v3;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Compilation;
using System.Web.Routing;
using System.Web.SessionState;

namespace wow_sync
{



    /// <summary>
    ///  This is a minimal implementation of Google+ Sign-In that
    ///  demonstrates:
    ///  - Using the Google+ Sign-In button to get an OAuth 2.0 refresh token.
    ///  - Exchanging the refresh token for an access token.
    ///  - Making Google+ API requests with the access token, including
    ///    getting a list of people that the user has circled.
    ///  - Disconnecting the app from the user's Google account and revoking
    ///    tokens.
    /// </summary>
    /// @author class@google.com (Gus Class)
    public class Signin : IHttpHandler, IRequiresSessionState, IRouteHandler
    {
        // These come from the APIs console:
        //   https://code.google.com/apis/console
        public static ClientSecrets secrets = new ClientSecrets()
        {
            ClientId = "YOUR_CLIENT_ID",
            ClientSecret = "YOUR_CLIENT_SECRET"
        };
        const string auth_jsion = @"{""installed"":{""client_id"":""812813560883-rjgtigdh6fli27mljvapl6v58h93t2j1.apps.googleusercontent.com"",""project_id"":""aep-simplesite"",""auth_uri"":""https://accounts.google.com/o/oauth2/auth"",""token_uri"":""https://accounts.google.com/o/oauth2/token"",""auth_provider_x509_cert_url"":""https://www.googleapis.com/oauth2/v1/certs"",""client_secret"":""plyqqJGWk-G2B-gKkyEAq9P4"",""redirect_uris"":[""urn:ietf:wg:oauth:2.0:oob"",""http://localhost""]}}";
        const string auth_name = "WowSyncAlpha";
        const string auth_id = "812813560883-rjgtigdh6fli27mljvapl6v58h93t2j1.apps.googleusercontent.com";

        // Configuration that you probably don't need to change.
        static public string APP_NAME = "Google+ C# Quickstart";
        static public string[] SCOPES = { DriveService.Scope.Drive };
        // Uncomment to retrieve email.
        //static public string[] SCOPES = { PlusService.Scope.PlusLogin, PlusService.Scope.UserinfoEmail };

        // Stores token response info such as the access token and refresh token.
        private TokenResponse token;
        
        // Used to peform API calls against Google+.
        private DriveService ps = null;

        /// <summary>
        /// Processes the request based on the path.
        /// </summary>
        /// <param name="context">Contains the request and response.</param>
        public void ProcessRequest(HttpContext context)
        {
            // Redirect base path to signin.
            if (context.Request.Path.EndsWith("/"))
            {
                context.Response.RedirectPermanent("signin.ashx");
            }

            // This is reached when the root document is passed. Return HTML
            // using index.html as a template.
            if (context.Request.Path.EndsWith("/signin.ashx"))
            {
                String state = (String)context.Session["state"];

                // Store a random string in the session for verifying
                // the responses in our OAuth2 flow.
                if (state == null)
                {
                    Random random = new Random((int)DateTime.Now.Ticks);
                    StringBuilder builder = new StringBuilder();
                    for (int i = 0; i < 13; i++)
                    {
                        builder.Append(Convert.ToChar(
                                Convert.ToInt32(Math.Floor(
                                        26 * random.NextDouble() + 65))));
                    }
                    state = builder.ToString();
                    context.Session["state"] = state;
                }

                // Render the templated HTML.
                String templatedHTML = File.ReadAllText(
                     context.Server.MapPath("index.html"));
                templatedHTML = Regex.Replace(templatedHTML,
                    "[{]{2}\\s*APPLICATION_NAME\\s*[}]{2}", APP_NAME);
                templatedHTML = Regex.Replace(templatedHTML,
                    "[{]{2}\\s*CLIENT_ID\\s*[}]{2}", secrets.ClientId);
                templatedHTML = Regex.Replace(templatedHTML,
                    "[{]{2}\\s*STATE\\s*[}]{2}", state);

                context.Response.ContentType = "text/html";
                context.Response.Write(templatedHTML);
                return;
            }

            if (context.Session["authState"] == null)
            {
                // The connect action exchanges a code from the sign-in button,
                // verifies it, and creates OAuth2 credentials.
                if (context.Request.Path.Contains("/connect"))
                {
                    // Get the code from the request POST body.
                    StreamReader sr = new StreamReader(
                        context.Request.InputStream);
                    string code = sr.ReadToEnd();

                    string state = context.Request["state"];

                    // Test that the request state matches the session state.
                    if (!state.Equals(context.Session["state"]))
                    {
                        context.Response.StatusCode = 401;
                        return;
                    }

                    // Use the code exchange flow to get an access and refresh token.
                    IAuthorizationCodeFlow flow =
                        new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                        {
                            ClientSecrets = secrets,
                            Scopes = SCOPES
                        });

                    token = flow.ExchangeCodeForTokenAsync("", code, "postmessage",
                            CancellationToken.None).Result;

                    // Create an authorization state from the returned token.
                    context.Session["authState"] = token;

                    // Get tokeninfo for the access token if you want to verify.
                    Oauth2Service service = new Oauth2Service(
                        new Google.Apis.Services.BaseClientService.Initializer());
                    Oauth2Service.TokeninfoRequest request = service.Tokeninfo();
                    request.AccessToken = token.AccessToken;

                    Tokeninfo info = request.Execute();

                    string gplus_id = info.UserId;
                }
                else
                {
                    // No cached state and we are not connecting.
                    context.Response.StatusCode = 400;
                    return;
                }
            }
            else if (context.Request.Path.Contains("/connect"))
            {
                // The user is already connected and credentials are cached.
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 200;
                context.Response.Write(JsonConvert.SerializeObject("Current user is already connected."));
                return;
            }
            else
            {
                // Register the authenticator and construct the Plus service
                // for performing API calls on behalf of the user.
                token = (TokenResponse)context.Session["authState"];
                IAuthorizationCodeFlow flow =
                    new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                    {
                        ClientSecrets = secrets,
                        Scopes = SCOPES
                    });

                UserCredential credential = new UserCredential(flow, "me", token);
                bool success = credential.RefreshTokenAsync(CancellationToken.None).Result;

                token = credential.Token;
                ps = new DriveService(
                    new Google.Apis.Services.BaseClientService.Initializer()
                    {
                        ApplicationName = ".NET Quickstart",
                        HttpClientInitializer = credential
                    });
            }

            // Perform an authenticated API request to retrieve the list of
            // people that the user has made visible to the app.
            if (context.Request.Path.Contains("/drive"))
            {
                // Get the PeopleFeed for the currently authenticated user.
             //   PeopleFeed pf = ps.People.List("me",
               //         PeopleResource.ListRequest.CollectionEnum.Visible).Execute();

                // This JSON, representing the people feed, will later be
                // parsed by the JavaScript client.
            //    string jsonContent =
               //     Newtonsoft.Json.JsonConvert.SerializeObject(pf);
            //    context.Response.ContentType = "application/json";
            //    context.Response.Write(jsonContent);
                return;
            }

            // Disconnect the user from the application by revoking the tokens
            // and removing all locally stored data associated with the user.
            if (context.Request.Path.Contains("/disconnect"))
            {
                // Perform a get request to the token endpoint to revoke the
                // refresh token.
                token = (TokenResponse)context.Session["authState"];
                string tokenToRevoke = (token.RefreshToken != null) ?
                    token.RefreshToken : token.AccessToken;

                WebRequest request = WebRequest.Create(
                    "https://accounts.google.com/o/oauth2/revoke?token=" +
                    tokenToRevoke);

                WebResponse response = request.GetResponse();

                // Remove the cached credentials.
                context.Session["authState"] = null;

                // You could reset the state in the session but you must also
                // reset the state on the client.
                // context.Session["state"] = null;
                context.Response.Write(
                    response.GetResponseStream().ToString().ToCharArray());
                return;
            }
        }

        /// <summary>
        /// Implements IRouteHandler interface for mapping routes to this
        /// IHttpHandler.
        /// </summary>
        /// <param name="requestContext">Information about the request.
        /// </param>
        /// <returns></returns>
        public IHttpHandler GetHttpHandler(RequestContext
            requestContext)
        {
            var page = BuildManager.CreateInstanceFromVirtualPath
                 ("~/signin.ashx", typeof(IHttpHandler)) as IHttpHandler;
            return page;
        }

        public bool IsReusable { get { return false; } }
    }
}
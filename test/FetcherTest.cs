using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using Moq;
using NUnit.Framework;

namespace LastPass.Test
{
    [TestFixture]
    class FetcherTest
    {
        private const string WebExceptionMessage = "WebException occured";
        private const string UnknownEmailMessage = "Invalid username";
        private const string InvalidPasswordMessage = "Invalid password";
        private const string MissingGoogleAuthenticatorCodeMessage = "Google Authenticator code is missing";
        private const string IncorrectGoogleAuthenticatorCodeMessage = "Google Authenticator code is incorrect";
        private const string MissingYubikeyPasswordMessage = "Yubikey password is missing or incorrect";
        private const string IncorrectYubikeyPasswordMessage = "Yubikey password is missing or incorrect";
        private const string OutOfBandAuthenticationRequiredMessage = "Out of band authentication required";
        private const string OutOfBandAuthenticationFailedMessage = "Out of band authentication failed";
        private const string OtherCause = "othercause";
        private const string OtherReasonMessage = "Other reason";
        private const string UnknownReasonMessage = "Unknown reason";
        private const string UnknownResponseSchemaMessage = "Unknown response schema";
        private const string InvalidXmlMessage = "Invalid XML in response";
        private const string InvalidBase64Message = "Invalid base64 in response";

        private const string LoginUrl = "https://lastpass.com/login.php";
        private const string AccoutDownloadUrl = "https://lastpass.com/getaccts.php?mobile=1&b64=1&hash=0.0";
        private const string Username = "username";
        private const string Password = "password";
        private const int InitialIterationCount = 1;
        private const int CorrectIterationCount = 5000;
        private const string GoogleAuthenticatorCode = "123456";
        private const string IncorrectGoogleAuthenticatorCode = "654321";
        private const string YubikeyPassword = "emdbwzemyisymdnevznyqhqnklaqheaxszzvtnxjrmkb";
        private const string IncorrectYubikeyPassword = "qlzpirxbsmanfzydaqlkcmiydzmhqjfemruyzyqhmray";
        private const string SessionId = "53ru,Hb713QnEVM5zWZ16jMvxS0";

        private static readonly string OkResponse = string.Format("<ok sessionid=\"{0}\" />", SessionId);

        private static readonly NameValueCollection SharedExpectedValues = new NameValueCollection
            {
                {"method", "mobile"},
                {"web", "1"},
                {"xml", "1"},
                {"username", Username}
            };

        private static readonly NameValueCollection ExpectedValues1 = new NameValueCollection(SharedExpectedValues)
            {
                {"hash", "e379d972c3eb59579abe3864d850b5f54911544adfa2daf9fb53c05d30cdc985"},
                {"iterations", string.Format("{0}", InitialIterationCount)}
            };

        private static readonly NameValueCollection ExpectedValues2 = new NameValueCollection(SharedExpectedValues)
            {
                {"hash", "7880a04588cfab954aa1a2da98fd9c0d2c6eba4c53e36a94510e6dbf30759256"},
                {"iterations", string.Format("{0}", CorrectIterationCount)}
            };

        private static readonly NameValueCollection ExpectedValuesGA1 = new NameValueCollection(ExpectedValues1)
            {
                {"otp", GoogleAuthenticatorCode}
            };

        private static readonly NameValueCollection ExpectedValuesGA2 = new NameValueCollection(ExpectedValues2)
            {
                {"otp", GoogleAuthenticatorCode}
            };

        private static readonly NameValueCollection ExpectedValuesYubikey1 = new NameValueCollection(ExpectedValues1)
            {
                {"otp", YubikeyPassword}
            };

        private static readonly NameValueCollection ExpectedValuesYubikey2 = new NameValueCollection(ExpectedValues2)
            {
                {"otp", YubikeyPassword}
            };

        //
        // Login
        //

        [Test]
        public void Login_failed_because_of_WebException()
        {
            // Immitate WebException
            var webException = new WebException();
            var webClient = new Mock<IWebClient>();
            webClient
                .Setup(x => x.UploadValues(It.IsAny<string>(),
                                           It.IsAny<NameValueCollection>()))
                .Throws(webException);

            // Login is supposed to rethrow as LoginException
            var e = Assert.Throws<LoginException>(() => Fetcher.Login(Username,
                                                                      Password,
                                                                      null,
                                                                      webClient.Object));

            // Verify the exception is set up correctly
            Assert.AreEqual(LoginException.FailureReason.WebException, e.Reason);
            Assert.AreEqual(WebExceptionMessage, e.Message);
            Assert.AreSame(webException, e.InnerException);
        }

        [Test]
        public void Login_failed_because_of_unknown_email()
        {
            LoginAndVerifyException(
                "<response><error message=\"Unknown email address.\" cause=\"unknownemail\" /></response>",
                LoginException.FailureReason.LastPassInvalidUsername,
                UnknownEmailMessage);
        }

        [Test]
        public void Login_failed_because_of_invalid_password()
        {
            LoginAndVerifyException(
                "<response><error message=\"Invalid password!\" cause=\"unknownpassword\" /></response>",
                LoginException.FailureReason.LastPassInvalidPassword,
                InvalidPasswordMessage);
        }

        [Test]
        public void Login_failed_because_of_missing_google_authenticator_code()
        {
            LoginAndVerifyException(
                "<response>" +
                    "<error " +
                        "message=\"Google Authenticator authentication required! Upgrade your browser extension so you can enter it.\" " +
                        "cause=\"googleauthrequired\" " +
                    "/>" +
                "</response>",
                LoginException.FailureReason.LastPassMissingGoogleAuthenticatorCode,
                MissingGoogleAuthenticatorCodeMessage);
        }

        [Test]
        public void Login_failed_because_of_incorrect_google_authenticator_code()
        {
            LoginAndVerifyException(
                IncorrectGoogleAuthenticatorCode,
                "<response>" +
                    "<error " +
                        "message=\"Google Authenticator authentication failed!\" " +
                        "cause=\"googleauthfailed\" " +
                    "/>" +
                "</response>",
                LoginException.FailureReason.LastPassIncorrectGoogleAuthenticatorCode,
                IncorrectGoogleAuthenticatorCodeMessage);
        }

        [Test]
        public void Login_failed_because_of_missing_yubikey_password()
        {
            LoginAndVerifyException(
                "<response>" +
                    "<error " +
                        "message=\"Your account settings have restricted you from logging in from mobile devices that do not support YubiKey authentication.\" " +
                        "cause=\"yubikeyrestricted\" " +
                    "/>" +
                "</response>",
                LoginException.FailureReason.LastPassIncorrectYubikeyPassword,
                MissingYubikeyPasswordMessage);
        }

        [Test]
        public void Login_failed_because_of_incorrect_yubikey_password()
        {
            LoginAndVerifyException(
                IncorrectYubikeyPassword,
                "<response>" +
                    "<error " +
                        "message=\"Your account settings have restricted you from logging in from mobile devices that do not support YubiKey authentication.\" " +
                        "cause=\"yubikeyrestricted\" " +
                    "/>" +
                "</response>",
                LoginException.FailureReason.LastPassIncorrectYubikeyPassword,
                IncorrectYubikeyPasswordMessage);
        }

        [Test]
        public void Login_failed_because_out_of_band_authentication_required()
        {
            LoginAndVerifyException(
                "<response>" +
                    "<error " +
                        "message=\"Multifactor authentication required! Upgrade your browser extension so you can enter it.\" " +
                        "cause=\"outofbandrequired\" " +
                        "retryid=\"2091457e-0ae8-4bee-948c-345afb49a132\" " +
                    "/>" +
                "</response>",
                LoginException.FailureReason.LastPassOutOfBandAuthenticationRequired,
                OutOfBandAuthenticationRequiredMessage);
        }

        [Test]
        public void Login_failed_because_out_of_band_authentication_failed()
        {
            LoginAndVerifyException(
                "<response>" +
                    "<error " +
                        "message=\"Multifactor authentication failed!\" " +
                        "cause=\"multifactorresponsefailed\" " +
                        "type=\"outofband\" " +
                    "/>" +
                "</response>",
                LoginException.FailureReason.LastPassOutOfBandAuthenticationFailed,
                OutOfBandAuthenticationFailedMessage);
        }

        [Test]
        public void Login_failed_for_other_reason_with_message()
        {
            LoginAndVerifyException(
                string.Format("<response><error message=\"{0}\" cause=\"{1}\"/></response>",
                              OtherReasonMessage,
                              OtherCause),
                LoginException.FailureReason.LastPassOther,
                OtherReasonMessage);
        }

        [Test]
        public void Login_failed_for_other_reason_without_message()
        {
            LoginAndVerifyException(
                string.Format("<response><error cause=\"{0}\"/></response>", OtherCause),
                LoginException.FailureReason.LastPassOther,
                OtherCause);
        }

        [Test]
        public void Login_failed_with_message_without_cause()
        {
            LoginAndVerifyException(
                string.Format("<response><error message=\"{0}\"/></response>", OtherReasonMessage),
                LoginException.FailureReason.LastPassOther,
                OtherReasonMessage);
        }

        [Test]
        public void Login_failed_for_unknown_reason_with_error_element()
        {
            LoginAndVerifyException(
                "<response><error /></response>",
                LoginException.FailureReason.LastPassUnknown,
                UnknownReasonMessage);
        }

        [Test]
        public void Login_failed_because_of_unknown_xml_schema()
        {
            LoginAndVerifyException(
                "<response />",
                LoginException.FailureReason.UnknownResponseSchema,
                UnknownResponseSchemaMessage);
        }

        [Test]
        public void Login_failed_because_of_invalid_xml()
        {
            // TODO: Verify the inner exception is set!
            LoginAndVerifyException(
                "Invalid XML!",
                LoginException.FailureReason.InvalidResponse,
                InvalidXmlMessage);
        }

        [Test]
        public void Login_requests_with_correct_values()
        {
            LoginAndVerify(null, ExpectedValues1);
        }

        [Test]
        public void Login_requests_with_correct_values_with_google_authenticator()
        {
            LoginAndVerify(GoogleAuthenticatorCode, ExpectedValuesGA1);
        }

        [Test]
        public void Login_requests_with_correct_values_with_yubikey()
        {
            LoginAndVerify(YubikeyPassword, ExpectedValuesYubikey1);
        }

        [Test]
        public void Login_rerequests_with_given_iterations()
        {
            LoginAndVerifyRerequest(null, ExpectedValues1, ExpectedValues2);
        }

        [Test]
        public void Login_rerequests_with_given_iterations_and_google_authenticator_password()
        {
            LoginAndVerifyRerequest(GoogleAuthenticatorCode, ExpectedValuesGA1, ExpectedValuesGA2);
        }

        [Test]
        public void Login_rerequests_with_given_iterations_and_yubikey_password()
        {
            LoginAndVerifyRerequest(YubikeyPassword, ExpectedValuesYubikey1, ExpectedValuesYubikey2);
        }

        //
        // Download
        //

        [Test]
        public void Fetch_sets_cookies()
        {
            var session = new Session(SessionId, CorrectIterationCount);
            var headers = new WebHeaderCollection();

            var webClient = new Mock<IWebClient>();
            webClient
                .SetupGet(x => x.Headers)
                .Returns(headers);

            Fetcher.Fetch(session, webClient.Object);

            Assert.AreEqual(string.Format("PHPSESSID={0}", Uri.EscapeDataString(SessionId)),
                            headers["Cookie"]);
        }

        [Test]
        public void Fetch_requests_accounts_from_correct_url()
        {
            var session = new Session(SessionId, CorrectIterationCount);
            var response = "VGVzdCBibG9i".ToBytes();
            var expectedBlob = "Test blob".ToBytes();

            var webClient = new Mock<IWebClient>();
            webClient
                .SetupGet(x => x.Headers)
                .Returns(new WebHeaderCollection());

            webClient
                .Setup(x => x.DownloadData(It.IsAny<string>()))
                .Returns(response);

            Fetcher.Fetch(session, webClient.Object);

            webClient.Verify(x => x.DownloadData(It.Is<string>(a => a == AccoutDownloadUrl)));
        }

        [Test]
        public void Fetch_returns_blob()
        {
            var session = new Session(SessionId, CorrectIterationCount);
            var response = "VGVzdCBibG9i".ToBytes();
            var expectedBlob = "Test blob".ToBytes();

            var webClient = new Mock<IWebClient>();
            webClient
                .SetupGet(x => x.Headers)
                .Returns(new WebHeaderCollection());

            webClient
                .Setup(x => x.DownloadData(It.IsAny<string>()))
                .Returns(response);

            var blob = Fetcher.Fetch(session, webClient.Object);

            Assert.AreEqual(expectedBlob, blob.Bytes);
            Assert.AreEqual(CorrectIterationCount, blob.KeyIterationCount);
        }

        [Test]
        public void Fetch_throws_on_WebException()
        {
            var session = new Session(SessionId, CorrectIterationCount);
            var webException = new WebException();

            var webClient = new Mock<IWebClient>();
            webClient
                .SetupGet(x => x.Headers)
                .Returns(new WebHeaderCollection());

            webClient
                .Setup(x => x.DownloadData(It.IsAny<string>()))
                .Throws(webException);

            var e = Assert.Throws<FetchException>(() => Fetcher.Fetch(session, webClient.Object));
            Assert.AreEqual(FetchException.FailureReason.WebException, e.Reason);
            Assert.AreEqual(WebExceptionMessage, e.Message);
            Assert.AreSame(webException, e.InnerException);
        }

        [Test]
        public void Fetch_throws_on_invalid_response()
        {
            var session = new Session(SessionId, CorrectIterationCount);

            var webClient = new Mock<IWebClient>();
            webClient
                .SetupGet(x => x.Headers)
                .Returns(new WebHeaderCollection());

            webClient
                .Setup(x => x.DownloadData(It.IsAny<string>()))
                .Returns("Invalid base64 string!".ToBytes());

            var e = Assert.Throws<FetchException>(() => Fetcher.Fetch(session, webClient.Object));
            Assert.AreEqual(FetchException.FailureReason.InvalidResponse, e.Reason);
            Assert.AreEqual(InvalidBase64Message, e.Message);
        }

        //
        // Helpers
        //

        private static void LoginAndVerifyException(string response,
                                                    LoginException.FailureReason reason,
                                                    string message)
        {
            LoginAndVerifyException(null, response, reason, message);
        }

        private static void LoginAndVerifyException(string multifactorPassword,
                                                    string response,
                                                    LoginException.FailureReason reason,
                                                    string message)
        {
            var webClient = new Mock<IWebClient>();
            webClient
                .Setup(x => x.UploadValues(It.IsAny<string>(), It.IsAny<NameValueCollection>()))
                .Returns(response.ToBytes());

            // Login is supposed to throw an exception
            var e = Assert.Throws<LoginException>(() => Fetcher.Login(Username,
                                                                      Password,
                                                                      multifactorPassword,
                                                                      webClient.Object));
            // Verify the exception is the one we're expecting
            Assert.AreEqual(reason, e.Reason);
            Assert.AreEqual(message, e.Message);
        }

        private static void LoginAndVerify(string multifactorPassword, NameValueCollection expectedValues)
        {
            // Simulate successful login
            var webClient = new Mock<IWebClient>();
            webClient
                .Setup(x => x.UploadValues(It.IsAny<string>(), It.IsAny<NameValueCollection>()))
                .Returns(OkResponse.ToBytes());

            Fetcher.Login(Username, Password, multifactorPassword, webClient.Object);

            // Verify the requests were made with appropriate POST values
            webClient.Verify(x => x.UploadValues(It.Is<string>(s => s == LoginUrl),
                                                 It.Is<NameValueCollection>(v => AreEqual(v, expectedValues))),
                             "Did not see POST request with expected values");
        }

        private static void LoginAndVerifyRerequest(string multifactorPassword,
                                                    NameValueCollection expectedValues1,
                                                    NameValueCollection expectedValues2)
        {
            // First LastPass responds with error, when PBKDF2 iteration count doesn't match
            var response1 = string.Format("<response><error iterations=\"{0}\" /></response>",
                                          CorrectIterationCount).ToBytes();

            // Seconds response is a success
            var response2 = OkResponse.ToBytes();

            var webClient = new Mock<IWebClient>();
            webClient
                .SetupSequence(x => x.UploadValues(It.IsAny<string>(), It.IsAny<NameValueCollection>()))
                .Returns(response1)
                .Returns(response2);

            var session = Fetcher.Login(Username, Password, multifactorPassword, webClient.Object);

            // Verify the requests were made with appropriate POST values
            // TODO: This doesn't check the order in which calls were made. Fix this!
            webClient.Verify(x => x.UploadValues(It.Is<string>(s => s == LoginUrl),
                                                 It.Is<NameValueCollection>(v => AreEqual(v, expectedValues1))),
                             "Did not see POST request with expected values 1");
            webClient.Verify(x => x.UploadValues(It.Is<string>(s => s == LoginUrl),
                                                 It.Is<NameValueCollection>(v => AreEqual(v, expectedValues2))),
                             "Did not see POST request with expected values 2");

            Assert.AreEqual(SessionId, session.Id);
        }

        private static bool AreEqual(NameValueCollection a, NameValueCollection b)
        {
            return a.AllKeys.OrderBy(s => s).SequenceEqual(b.AllKeys.OrderBy(s => s)) &&
                   a.AllKeys.All(s => a[s] == b[s]);
        }
    }
}

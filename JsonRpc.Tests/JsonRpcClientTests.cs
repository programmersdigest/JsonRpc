using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace programmersdigest.JsonRpc.Tests
{
    [TestClass]
    public class JsonRpcClientTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_SendCallbackIsNull_ShouldThrowArgumentNullException()
        {
            Action<byte[]> sendCallback = null;
            Func<byte[]> receiveCallback = () => new byte[0];

            new JsonRpcClient(sendCallback, receiveCallback);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_ReceiveCallbackIsNull_ShouldThrowArgumentNullException()
        {
            Action<byte[]> sendCallback = (d) => { };
            Func<byte[]> receiveCallback = null;

            new JsonRpcClient(sendCallback, receiveCallback);
        }

        [DataTestMethod]
        [DataRow("Test Method", null, @"{""jsonrpc"":""2.0"",""method"":""Test Method""}")]
        [DataRow("Test Method 3", new object[0], @"{""jsonrpc"":""2.0"",""method"":""Test Method 3""}")]
        [DataRow("Test Method 2", new object[] { 123, 456.789, "Test String" }, @"{""jsonrpc"":""2.0"",""method"":""Test Method 2"",""params"":[123,456.789,""Test String""]}")]
        public void Notify_ValidArguments_ShouldSendRequestWithArguments(string method, object[] parameters, string expectedJson)
        {
            string sentJson = null;

            void sendCallback(byte[] data)
            {
                sentJson = Encoding.UTF8.GetString(data);
            }

            byte[] receiveCallback()
            {
                return new byte[0];
            }

            var jsonRpcClient = new JsonRpcClient(sendCallback, receiveCallback);

            if (parameters == null)
            {
                jsonRpcClient.Notify(method);
            }
            else
            {
                jsonRpcClient.Notify(method, parameters);
            }

            Assert.AreEqual(expectedJson, sentJson);
        }

        [DataTestMethod]
        [DataRow("Test Method", null, @"{""jsonrpc"":""2.0"",""method"":""Test Method"",""id"":""@id""}")]
        [DataRow("Test Method 3", new object[0], @"{""jsonrpc"":""2.0"",""method"":""Test Method 3"",""id"":""@id""}")]
        [DataRow("Test Method 2", new object[] { 123, 456.789, "Test String" }, @"{""jsonrpc"":""2.0"",""method"":""Test Method 2"",""params"":[123,456.789,""Test String""],""id"":""@id""}")]
        public void Call_ValidArguments_ShouldSendRequestWithArguments(string method, object[] parameters, string expectedJson)
        {
            string sentJson = null;
            string sentId = null;

            void sendCallback(byte[] data)
            {
                sentJson = Encoding.UTF8.GetString(data);
                sentId = Regex.Match(sentJson, @"""id"":""(.*?)""").Groups[1].Value;
            }

            byte[] receiveCallback()
            {
                if (sentId != null)
                {
                    return Encoding.UTF8.GetBytes(@"{""jsonrpc"":""2.0"",""result"":""Test"",""id"":""" + sentId + @"""}");
                }

                return new byte[0];
            }

            var jsonRpcClient = new JsonRpcClient(sendCallback, receiveCallback);

            if (parameters == null)
            {
                jsonRpcClient.Call(method);
            }
            else
            {
                jsonRpcClient.Call(method, parameters);
            }

            expectedJson = expectedJson.Replace("@id", sentId);
            Assert.AreEqual(expectedJson, sentJson);
        }

        [TestMethod]
        public void Receive_MethodDoesNotExist_ShouldReturnMethodNotFoundError()
        {
            bool requestReceived = false;

            var responseSentEvent = new ManualResetEvent(false);
            string sentResponse = null;

            void sendCallback(byte[] data)
            {
                sentResponse = Encoding.UTF8.GetString(data);
                responseSentEvent.Set();
            }

            byte[] receiveCallback()
            {
                if (!requestReceived)
                {
                    requestReceived = true;
                    return Encoding.UTF8.GetBytes(@"{""jsonrpc"":""2.0"",""method"":""test"",""id"":123}");
                }

                return new byte[0];
            }

            var jsonRpcClient = new JsonRpcClient(sendCallback, receiveCallback);

            responseSentEvent.WaitOne();
            Assert.AreEqual(@"{""jsonrpc"":""2.0"",""error"":{""code"":-32601,""message"":""Method test could not be found.""},""id"":123}",
                            sentResponse);
        }

        [TestMethod]
        public void Receive_RequestArrayIsEmpty_ShouldReturnSingleInvalidRequestError()
        {
            bool requestReceived = false;

            var responseSentEvent = new ManualResetEvent(false);
            string sentResponse = null;

            void sendCallback(byte[] data)
            {
                sentResponse = Encoding.UTF8.GetString(data);
                responseSentEvent.Set();
            }

            byte[] receiveCallback()
            {
                if (!requestReceived)
                {
                    requestReceived = true;
                    return Encoding.UTF8.GetBytes(@"[]");
                }

                return new byte[0];
            }

            var jsonRpcClient = new JsonRpcClient(sendCallback, receiveCallback);

            responseSentEvent.WaitOne();
            Assert.AreEqual(@"{""jsonrpc"":""2.0"",""error"":{""code"":-32600,""message"":""Request array must not be empty.""}}",
                            sentResponse);
        }

        [TestMethod]
        public void Receive_RequestArrayHasInvalidEntries_ShouldReturnInvalidRequestErrorPerEntry()
        {
            bool requestReceived = false;

            var responseSentEvent = new ManualResetEvent(false);
            string sentResponse = null;

            void sendCallback(byte[] data)
            {
                sentResponse = Encoding.UTF8.GetString(data);
                responseSentEvent.Set();
            }

            byte[] receiveCallback()
            {
                if (!requestReceived)
                {
                    requestReceived = true;
                    return Encoding.UTF8.GetBytes(@"[1,2,3]");
                }

                return new byte[0];
            }

            var jsonRpcClient = new JsonRpcClient(sendCallback, receiveCallback);

            responseSentEvent.WaitOne();
            Assert.AreEqual(@"[{""jsonrpc"":""2.0"",""error"":{""code"":-32600,""message"":""The message is not a valid request object.""}},{""jsonrpc"":""2.0"",""error"":{""code"":-32600,""message"":""The message is not a valid request object.""}},{""jsonrpc"":""2.0"",""error"":{""code"":-32600,""message"":""The message is not a valid request object.""}}]",
                            sentResponse);
        }

        [TestMethod]
        public void Receive_MethodHasDifferentParameters_ShouldReturnInvalidParametersError()
        {
            bool requestReceived = false;

            var methodRegisteredEvent = new ManualResetEvent(false);
            var responseSentEvent = new ManualResetEvent(false);
            string sentResponse = null;

            void sendCallback(byte[] data)
            {
                sentResponse = Encoding.UTF8.GetString(data);
                responseSentEvent.Set();
            }

            byte[] receiveCallback()
            {
                methodRegisteredEvent.WaitOne();
                if (!requestReceived)
                {
                    requestReceived = true;
                    return Encoding.UTF8.GetBytes(@"{""jsonrpc"":""2.0"",""method"":""TestMethod"",""params"":[123,""TestParam""],""id"":123}");
                }

                return new byte[0];
            }

            var jsonRpcClient = new JsonRpcClient(sendCallback, receiveCallback);
            jsonRpcClient.Register("TestMethod", new Action<string, int>((s, i) => { }));
            methodRegisteredEvent.Set();

            responseSentEvent.WaitOne();
            Assert.AreEqual(@"{""jsonrpc"":""2.0"",""error"":{""code"":-32602,""message"":""The provided parameters do not match the registered method.""},""id"":123}",
                            sentResponse);
        }

        [TestMethod]
        public void Receive_MethodHasDifferentNumberOfParameters_ShouldReturnInvalidParametersError()
        {
            bool requestReceived = false;

            var methodRegisteredEvent = new ManualResetEvent(false);
            var responseSentEvent = new ManualResetEvent(false);
            string sentResponse = null;

            void sendCallback(byte[] data)
            {
                sentResponse = Encoding.UTF8.GetString(data);
                responseSentEvent.Set();
            }

            byte[] receiveCallback()
            {
                methodRegisteredEvent.WaitOne();
                if (!requestReceived)
                {
                    requestReceived = true;
                    return Encoding.UTF8.GetBytes(@"{""jsonrpc"":""2.0"",""method"":""TestMethod"",""params"":[123,""TestParam""],""id"":123}");
                }

                return new byte[0];
            }

            var jsonRpcClient = new JsonRpcClient(sendCallback, receiveCallback);
            jsonRpcClient.Register("TestMethod", new Action<int>((i) => { }));
            methodRegisteredEvent.Set();

            responseSentEvent.WaitOne();
            Assert.AreEqual(@"{""jsonrpc"":""2.0"",""error"":{""code"":-32602,""message"":""The method has a different parameter count.""},""id"":123}",
                            sentResponse);
        }
    }
}

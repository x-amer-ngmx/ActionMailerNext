﻿#region License
/* Copyright (C) 2011 by Scott W. Anderson
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
#endregion

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using FakeItEasy;
using Xunit;
using System.Text;

namespace ActionMailer.Net.Tests.Mvc {
    public class MailerBaseTests {
        [Fact]
        public void EmailMethodShouldSetPropertiesOnMailMessage() {
            var mailer = new TestMailerBase();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new TextViewEngine());
            mailer.HttpContextBase = new EmptyHttpContextBase();
            mailer.To.Add("test@test.com");
            mailer.From = "no-reply@mysite.com";
            mailer.Subject = "test subject";
            mailer.CC.Add("test-cc@test.com");
            mailer.BCC.Add("test-bcc@test.com");
            mailer.ReplyTo.Add("test-reply-to@test.com");
            mailer.Headers.Add("X-No-Spam", "True");

            var result = mailer.Email("TestView");

            Assert.Equal("test@test.com", result.Mail.To[0].Address);
            Assert.Equal("no-reply@mysite.com", result.Mail.From.Address);
            Assert.Equal("test subject", result.Mail.Subject);
            Assert.Equal("test-cc@test.com", result.Mail.CC[0].Address);
            Assert.Equal("test-bcc@test.com", result.Mail.Bcc[0].Address);
            Assert.Equal("test-reply-to@test.com", result.Mail.ReplyToList[0].Address);
            Assert.Equal("True", result.Mail.Headers["X-No-Spam"]);
        }

        [Fact]
        public void PassingAMailSenderShouldWork() {
            var mockSender = A.Fake<IMailSender>();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new TextViewEngine());

            var mailer = new TestMailerBase(mockSender);
            mailer.HttpContextBase = new EmptyHttpContextBase();
            mailer.From = "no-reply@mysite.com";
            var result = mailer.Email("TestView");

            Assert.Same(mockSender, mailer.MailSender);
            Assert.Same(mockSender, result.MailSender);
        }

        [Fact]
        public void ViewBagDataShouldCopyToEmailResult() {
            var mailer = new TestMailerBase();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new TextViewEngine());
            mailer.HttpContextBase = new EmptyHttpContextBase();
            mailer.From = "no-reply@mysite.com";

            mailer.ViewBag.Test = "12345";
            var result = mailer.Email("TestView");

            Assert.Equal("12345", result.ViewBag.Test);
        }

        [Fact]
        public void ModelObjectShouldCopyToEmailResult() {
            var mailer = new TestMailerBase();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new TextViewEngine());
            mailer.HttpContextBase = new EmptyHttpContextBase();
            mailer.From = "no-reply@mysite.com";

            object model = "12345";
            var result = mailer.Email("TestView", model);

            Assert.Same(model, result.ViewData.Model);
        }

        [Fact]
        public void EmailMethodShouldRenderViewAsMessageBody() {
            var mailer = new TestMailerBase();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new TextViewEngine());
            mailer.HttpContextBase = new EmptyHttpContextBase();
            mailer.From = "no-reply@mysite.com";

            // there's no need to test the built-in view engines.
            // this test just ensures that our Email() method actually
            // populates the mail body properly.
            var result = mailer.Email("TestView");
            var reader = new StreamReader(result.Mail.AlternateViews[0].ContentStream);
            var body = reader.ReadToEnd().Trim();

            Assert.Equal("TextView", body);
        }

        [Fact]
        public void MessageEncodingOverrideShouldWork() {
            var mailer = new TestMailerBase();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new UTF8ViewEngine());
            mailer.HttpContextBase = new EmptyHttpContextBase();
            mailer.From = "no-reply@mysite.com";
            mailer.MessageEncoding = Encoding.UTF8;

            var result = mailer.Email("TestView");
            var reader = new StreamReader(result.Mail.AlternateViews[0].ContentStream);
            var body = reader.ReadToEnd();

            Assert.Equal(Encoding.UTF8, result.MessageEncoding);
            Assert.Equal("Umlauts are Über!", body);
        }

        [Fact]
        public void EmailMethodShouldAllowMultipleViews() {
            var mailer = new TestMailerBase();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new MultipartViewEngine());
            mailer.HttpContextBase = new EmptyHttpContextBase();
            mailer.From = "no-reply@mysite.com";

            // there's no need to test the built-in view engines.
            // this test just ensures that our Email() method actually
            // populates the mail body properly.
            var result = mailer.Email("TestView");

            Assert.Equal(2, result.Mail.AlternateViews.Count());

            var htmlReader = new StreamReader(result.Mail.AlternateViews[0].ContentStream);
            var htmlBody = htmlReader.ReadToEnd();
            Assert.Contains("HtmlView", htmlBody);
            Assert.Equal("text/html", result.Mail.AlternateViews[0].ContentType.MediaType);

            var textReader = new StreamReader(result.Mail.AlternateViews[1].ContentStream);
            var textBody = textReader.ReadToEnd();
            Assert.Contains("TextView", textBody);
            Assert.Equal("text/plain", result.Mail.AlternateViews[1].ContentType.MediaType);
        }

        [Fact]
        public void AttachmentsShouldAttachProperly() {
            var mailer = new TestMailerBase();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new TextViewEngine());
            mailer.HttpContextBase = new EmptyHttpContextBase();
            mailer.From = "no-reply@mysite.com";
            var imagePath = Path.Combine(Assembly.GetExecutingAssembly().FullName, "..", "..", "..", "SampleData", "logo.png");
            var imageBytes = File.ReadAllBytes(imagePath);
            mailer.Attachments["logo.png"] = imageBytes;
            mailer.Attachments.Inline["logo-inline.png"] = imageBytes;

            var result = mailer.Email("TestView");
            var attachment = result.Mail.Attachments[0];
            var inlineAttachment = result.Mail.Attachments[1];
            byte[] attachmentBytes = new byte[attachment.ContentStream.Length];
            byte[] inlineAttachmentBytes = new byte[inlineAttachment.ContentStream.Length];
            attachment.ContentStream.Read(attachmentBytes, 0, Convert.ToInt32(attachment.ContentStream.Length));
            inlineAttachment.ContentStream.Read(inlineAttachmentBytes, 0, Convert.ToInt32(inlineAttachment.ContentStream.Length));

            Assert.Equal("logo.png", attachment.Name);
            Assert.Equal("logo-inline.png", inlineAttachment.Name);
            Assert.Equal("image/png", attachment.ContentType.MediaType);
            Assert.Equal("image/png", inlineAttachment.ContentType.MediaType);
            Assert.Equal("logo.png", attachment.ContentId);
            Assert.Equal("logo-inline.png", inlineAttachment.ContentId);
            Assert.True(attachmentBytes.SequenceEqual(imageBytes));
            Assert.True(inlineAttachmentBytes.SequenceEqual(imageBytes));
            Assert.True(inlineAttachment.ContentDisposition.Inline);
        }

        [Fact]
        public void ViewNameShouldBePassedProperly() {
            var mailer = new TestMailController();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new TextViewEngine());
            mailer.HttpContextBase = new EmptyHttpContextBase();

            var email = mailer.TestMail();

            Assert.Equal("TestView", email.ViewName);
        }

        [Fact]
        public void MasterNameShouldBePassedProperly() {
            var mailer = new TestMailController();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new TextViewEngine());
            mailer.HttpContextBase = new EmptyHttpContextBase();

            var email = mailer.TestMaster();

            Assert.Equal("TestMaster", email.MasterName);
        }

        [Fact]
        public void ViewNameShouldBeRequiredWhenUsingCallingEmailMethod() {
            var mailer = new TestMailerBase();
            mailer.HttpContextBase = new EmptyHttpContextBase();

            Assert.Throws<ArgumentNullException>(() => {
                var email = mailer.Email(null);
            });
        }

        [Fact]
        public void AreasAreDetectedProperly() {
            var mailer = new Areas.TestArea.Controllers.MailController();
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new TextViewEngine());
            mailer.HttpContextBase = new EmptyHttpContextBase();

            var email = mailer.TestEmail();

            Assert.NotNull(mailer.ControllerContext.RouteData.DataTokens["area"]);
            Assert.Equal("TestArea", mailer.ControllerContext.RouteData.DataTokens["area"]);
        }
    }
}

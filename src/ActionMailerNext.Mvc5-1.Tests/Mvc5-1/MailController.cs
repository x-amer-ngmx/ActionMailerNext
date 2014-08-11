﻿using System.Net.Mail;

namespace ActionMailerNext.Mvc5_1.Tests
{
    public class MailController : TestMailerBase
    {
        public EmailResult TestEmail()
        {
            MailAttributes.From = new MailAddress("test@test.com");
            MailAttributes.Subject = "test subject";
            return Email("TestView");
        }
    }
}
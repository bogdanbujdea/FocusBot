# Focus Bot Browser Extension – Privacy Policy

**Last updated:** March 2025

This privacy policy describes how the Focus Bot browser extension ("the extension") handles data. The extension is published for Chrome and Edge and does not operate a separate website or backend server.

---

## 1. Data the extension uses

The extension uses the following data to provide its functionality (helping you see whether your browsing matches a single task you set):

- **Focus task text** – The task description you enter when starting a session (e.g. "code review" or "research"). Stored only on your device.
- **Browsing data for the active session** – For each tab you visit during a focus session, the extension uses:
  - **Page URL** and **page title**
  - **Time you entered** the page (timestamp)
- **Settings** – Your preferences, including any OpenAI API key you provide and domains you choose to exclude from classification. Stored only on your device.
- **Session history and summaries** – Completed session data and daily analytics (URLs, titles, timestamps, aligned/distracting labels) used to show summaries and analytics. Stored only on your device.

The extension does **not** collect your name, email, address, health or financial data, passwords, or personal communications. It does not log keystrokes or monitor activity outside of the browser tabs used during a focus session.

---

## 2. Where data is stored

All of the data above is stored **only on your device** in the browser’s local storage (Chrome/Edge storage APIs). It is **not** sent to any server operated by the extension’s developer. There is no account, no sign-in, and no backend database.

---

## 3. Third-party processing (OpenAI)

When you configure an **OpenAI API key** in the extension:

- The extension sends to **OpenAI’s API** only the data needed for classification: the **current page URL**, **page title**, and your **focus task text**.
- No other data (e.g. full page content, history from other sessions) is sent.
- Your API key is stored locally in the extension and is used only to authenticate requests to OpenAI. The extension developer does not have access to your API key or to the requests you make.
- OpenAI’s handling of data sent to their API is governed by **OpenAI’s privacy policy**: [https://openai.com/policies/privacy-policy](https://openai.com/policies/privacy-policy).

If you do **not** provide an API key, the extension does not send any data to the internet for classification (it will only show an error or prompt you to add a key).

---

## 4. How we use and do not use data

- Data is used **only** to provide the extension’s single purpose: to classify pages as aligned or distracting with your task, and to show session summaries and daily analytics.
- We **do not** sell or transfer user data to third parties for advertising, marketing, or any purpose other than the approved use (sending URL, title, and task to OpenAI when you have configured an API key).
- We **do not** use or transfer user data for purposes unrelated to the extension’s single purpose.
- We **do not** use or transfer user data to determine creditworthiness or for lending purposes.

---

## 5. Data retention and your control

- Data stays in your browser until you remove the extension or clear the extension’s storage. The extension does not retain data on any server.
- You can clear stored data at any time by removing the extension or by clearing site/extension data for the extension in your browser settings.

---

## 6. Changes to this policy

If this privacy policy changes, the updated version will be posted at the same URL. The "Last updated" date at the top will be revised. For the browser extension, we encourage you to check this page when you update the extension.

---

## 7. Contact

For questions about this privacy policy or the Focus Bot extension, please open an issue or contact the maintainers through the project’s repository or website.

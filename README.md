# Estimo – MVP

**Estimo** is a minimal viable product (MVP) for freelancers and small agencies to create client quotes, export them to PDF, and get paid online via Stripe — all in a few clicks.

🚀 **Flow**:  
`Client → Quote → PDF → Payment Link (Stripe Checkout)`

---

## ✨ Features (MVP)
- **Authentication & Authorization** (JWT)
- **Client Management**
  - Create and manage clients
- **Quote Management**
  - Generate quotes linked to clients
  - Export quote as professional PDF (via QuestPDF)
  - Automatically include online payment link
- **Billing & Payments**
  - Stripe Checkout integration
  - Free plan with usage limits (15 free quotes, then 5/day)
  - Subscription plans: `Pro` and `Business`
- **Usage Tracking**
  - Daily and total counters per user
  - Enforced limits depending on subscription
- **API & Frontend**
  - Backend: ASP.NET Core + EF Core + PostgreSQL
  - Frontend: React (Vite) + Fetch API
  - CORS-enabled, ready for deployment

---

## 🛠️ Tech Stack
- **Backend**: ASP.NET Core 8, Minimal APIs
- **Database**: PostgreSQL + Entity Framework Core
- **Auth**: JWT + ASP.NET Identity PasswordHasher
- **Billing**: Stripe API + Stripe Checkout Sessions
- **PDF Generation**: QuestPDF
- **Logging**: Serilog
- **Frontend**: React + TypeScript + Vite
- **Other**: Docker-ready, CORS, Rate Limiting

---

## 📸 Screenshots (MVP UI)
<img width="1113" height="521" alt="image" src="https://github.com/user-attachments/assets/47825d8e-603b-49ec-932c-95768da6b2b5" />
<img width="1082" height="575" alt="image" src="https://github.com/user-attachments/assets/b5bcfd96-131c-4865-b83f-ffe50eeaa80b" />
<img width="1102" height="371" alt="image" src="https://github.com/user-attachments/assets/4fbf573a-09d4-4d4a-8ae1-0fd0bb464750" />
<img width="1112" height="452" alt="image" src="https://github.com/user-attachments/assets/8fecf9a2-21a1-4e69-8ab8-216f913200be" />
<img width="776" height="406" alt="image" src="https://github.com/user-attachments/assets/acb9c773-25c8-464b-9fa8-9d65045be0d3" />
<img width="1920" height="946" alt="image" src="https://github.com/user-attachments/assets/310b6e3e-60fa-43ad-80c3-b72167e6c1c4" />


---

## 🚦 Current Limitations
- Basic UI (MVP-grade)
- Only EUR currency
- No multi-language support
- No production-ready error handling

---

## 🎯 Roadmap (Next Steps)
1. **Stability & Deployment**
   - Deploy backend (Render / Azure / Fly.io)
   - Deploy frontend (Vercel / Netlify)
   - Add CI/CD pipelines (GitHub Actions)

2. **Product Enhancements**
   - Editable quote items (not only amount + VAT)
   - Custom branding (logos, colors)
   - Multi-currency support
   - Invoice history & dashboards

3. **Billing Improvements**
   - Webhooks for automatic Stripe confirmation
   - Subscription lifecycle management (renewals, cancellations)
   - More granular plans (Starter, Pro, Business)

4. **User Growth**
   - Landing page with demo video
   - Public beta signups
   - Product Hunt / IndieHackers launch

5. **Future Vision**
   - AI-assisted quote generation
   - Integration with CRM tools (Hubspot, Notion, Trello)
   - Team collaboration (multi-user workspaces)

---

## 🚀 Why This Project
Freelancers often lose time formatting quotes, exporting PDFs, and chasing payments. **Estimo** eliminates the friction by combining all steps into a **single streamlined workflow**:
- Less admin work
- Faster payments
- Professional experience for clients

---

## 🤝 Contributing
This project is currently an MVP but open for collaboration.  
Ideas, issues, and pull requests are welcome.

---

## 📜 License
MIT License © 2025 Estimo

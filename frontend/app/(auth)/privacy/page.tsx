import Link from "next/link";
import { ArrowLeft } from "lucide-react";

export const metadata = { title: "Privacy Policy" };

export default function PrivacyPage() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="mx-auto max-w-3xl px-6 py-16">
        <Link
          href="/login"
          className="mb-8 inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" /> Back to sign in
        </Link>

        <h1 className="mb-2 text-3xl font-bold tracking-tight">Privacy Policy</h1>
        <p className="mb-10 text-sm text-muted-foreground">Last updated: April 2026</p>

        <div className="prose prose-neutral dark:prose-invert max-w-none space-y-8 text-sm leading-7 text-muted-foreground">
          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">1. Information We Collect</h2>
            <p>
              DeployFlow collects information you provide directly: account credentials (email and
              hashed password), deployment configurations, server connection details, and activity
              logs. We do not collect personally identifying information beyond what is necessary
              to operate the platform.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">2. How We Use Information</h2>
            <p>
              Information collected is used solely to operate, maintain, and improve the DeployFlow
              platform. This includes authentication, deployment execution, monitoring, and sending
              operational notifications such as alerts and deployment status updates.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">3. Data Storage</h2>
            <p>
              As a self-hosted platform, all your data is stored on infrastructure you control.
              DeployFlow does not transmit your deployment data, server credentials, or application
              secrets to external servers.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">4. Data Retention</h2>
            <p>
              Deployment logs, audit events, and metrics are retained according to the retention
              settings configured by your administrator. You may request deletion of your account
              data at any time through your account settings.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">5. Security</h2>
            <p>
              We implement industry-standard security measures including encrypted credentials,
              JWT-based authentication, role-based access control, and full audit logging. You are
              responsible for maintaining the security of your self-hosted infrastructure.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">6. Third-Party Integrations</h2>
            <p>
              DeployFlow may connect to third-party services such as GitHub, GitLab, or cloud
              providers based on your configuration. Your use of those integrations is subject to
              their respective privacy policies.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">7. Changes to This Policy</h2>
            <p>
              We may update this Privacy Policy periodically. We will notify you of significant
              changes through the platform. Continued use of DeployFlow after changes constitutes
              acceptance of the updated policy.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">8. Contact</h2>
            <p>
              Questions about this Privacy Policy should be directed to your system administrator
              or the{" "}
              <Link href="/terms" className="text-primary hover:underline">
                Terms of Service
              </Link>
              .
            </p>
          </section>
        </div>
      </div>
    </div>
  );
}

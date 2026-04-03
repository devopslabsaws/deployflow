import Link from "next/link";
import { ArrowLeft } from "lucide-react";

export const metadata = { title: "Terms of Service" };

export default function TermsPage() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="mx-auto max-w-3xl px-6 py-16">
        <Link
          href="/login"
          className="mb-8 inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" /> Back to sign in
        </Link>

        <h1 className="mb-2 text-3xl font-bold tracking-tight">Terms of Service</h1>
        <p className="mb-10 text-sm text-muted-foreground">Last updated: April 2026</p>

        <div className="prose prose-neutral dark:prose-invert max-w-none space-y-8 text-sm leading-7 text-muted-foreground">
          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">1. Acceptance of Terms</h2>
            <p>
              By accessing or using DeployFlow, you agree to be bound by these Terms of Service. If
              you do not agree to all of these terms, you may not use the platform.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">2. Use of the Service</h2>
            <p>
              DeployFlow is a self-hosted deployment platform. You are responsible for maintaining
              the security of your account credentials and for all activities that occur under your
              account. You agree not to use the service for any unlawful purpose or in any way that
              could damage, disable, or impair the platform.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">3. Data and Privacy</h2>
            <p>
              Your use of DeployFlow is also governed by our{" "}
              <Link href="/privacy" className="text-primary hover:underline">
                Privacy Policy
              </Link>
              , which is incorporated into these Terms by reference.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">4. Intellectual Property</h2>
            <p>
              All content, features, and functionality of DeployFlow are the exclusive property of
              DeployFlow and its licensors. You may not copy, modify, distribute, or create
              derivative works without explicit written permission.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">5. Limitation of Liability</h2>
            <p>
              DeployFlow is provided &quot;as is&quot; without warranties of any kind. In no event
              shall DeployFlow be liable for any indirect, incidental, special, or consequential
              damages arising from your use of the service.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">6. Changes to Terms</h2>
            <p>
              We reserve the right to modify these terms at any time. Continued use of the platform
              following any changes constitutes acceptance of the new terms.
            </p>
          </section>

          <section>
            <h2 className="mb-3 text-base font-semibold text-foreground">7. Contact</h2>
            <p>
              If you have any questions about these Terms, please contact your system administrator.
            </p>
          </section>
        </div>
      </div>
    </div>
  );
}

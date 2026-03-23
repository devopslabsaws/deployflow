/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'standalone',
  typescript: {
    // Type errors are caught by the IDE / CI type-check step, not the build.
    ignoreBuildErrors: true,
  },
  eslint: {
    ignoreDuringBuilds: true,
  },
  experimental: {
    serverComponentsExternalPackages: [],
  },
  images: {
    remotePatterns: [
      { protocol: 'https', hostname: 'avatars.githubusercontent.com' },
      { protocol: 'https', hostname: 'lh3.googleusercontent.com' },
      { protocol: 'https', hostname: 'github.com' },
    ],
  },
  async redirects() {
    return [
      { source: '/auth/login', destination: '/login', permanent: false },
      { source: '/auth/register', destination: '/register', permanent: false },
      { source: '/auth/forgot-password', destination: '/forgot-password', permanent: false },
    ];
  },
  async rewrites() {
    return [
      {
        source: '/proxy/:path*',
        // API_INTERNAL_URL is set by docker-compose (http://api:8080).
        // Falls back to local dev port when running outside Docker.
        destination: `${process.env.API_INTERNAL_URL || 'http://localhost:5001'}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;

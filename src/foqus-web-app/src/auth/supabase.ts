import { createClient } from "@supabase/supabase-js";

const defaultSupabaseUrl = import.meta.env.PROD
  ? "https://mokjfxtnqmudypnukqsv.supabase.co"
  : "";
const defaultSupabaseAnonKey = import.meta.env.PROD
  ? "sb_publishable_U9dKqMzxtpms_EGvvUybCg_IKGoPc3t"
  : "";

const supabaseUrl =
  (import.meta.env.VITE_SUPABASE_URL as string | undefined)?.trim() ||
  defaultSupabaseUrl;
const supabaseAnonKey =
  (import.meta.env.VITE_SUPABASE_ANON_KEY as string | undefined)?.trim() ||
  defaultSupabaseAnonKey;

if (!supabaseUrl || !supabaseAnonKey) {
  throw new Error(
    "Missing VITE_SUPABASE_URL or VITE_SUPABASE_ANON_KEY environment variables"
  );
}

export const supabase = createClient(supabaseUrl, supabaseAnonKey, {
  auth: {
    flowType: "pkce",
    detectSessionInUrl: true,
    autoRefreshToken: true,
    persistSession: true,
  },
});

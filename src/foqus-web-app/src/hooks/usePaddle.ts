import { initializePaddle, type Paddle } from "@paddle/paddle-js";
import { useCallback, useEffect, useRef, useState } from "react";
import { fetchPricingPublic } from "../api/client";
import type { PricingResponse } from "../api/types";

export function usePaddle(onCheckoutCompleted?: () => void) {
  const [pricing, setPricing] = useState<PricingResponse | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const paddleRef = useRef<Paddle | undefined>(undefined);
  const onCompleteRef = useRef(onCheckoutCompleted);
  onCompleteRef.current = onCheckoutCompleted;

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const p = await fetchPricingPublic();
      if (cancelled) return;
      if (!p?.plans?.length) {
        setLoadError("Pricing is unavailable. Check API and Paddle configuration.");
        return;
      }
      setPricing(p);

      const token = p.clientToken?.trim();
      if (!token) {
        setLoadError(
          "Plans are loaded, but checkout cannot start: the API returned no Paddle client token. Set Paddle:ClientToken on FocusBot.WebAPI (sandbox: Paddle Dashboard → Developer tools → Authentication → client token)."
        );
        return;
      }

      try {
        const instance = await initializePaddle({
          environment: p.isSandbox ? "sandbox" : "production",
          token,
          eventCallback: (event) => {
            if (event.name === "checkout.completed") {
              onCompleteRef.current?.();
            }
          },
        });
        if (cancelled) return;
        paddleRef.current = instance;
        setReady(!!instance);
      } catch (e) {
        if (!cancelled) {
          setLoadError(
            e instanceof Error ? e.message : "Failed to initialize Paddle checkout."
          );
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const openCheckout = useCallback(
    (priceId: string, planTypeSlug: string, email: string | undefined, userId: string) => {
      const paddle = paddleRef.current;
      if (!paddle) return;
      paddle.Checkout.open({
        items: [{ priceId, quantity: 1 }],
        customer: email ? { email } : undefined,
        customData: { user_id: userId, plan_type: planTypeSlug },
        settings: { displayMode: "overlay", theme: "light" },
      });
    },
    []
  );

  return { pricing, loadError, ready, openCheckout };
}

import React from "react";

// Test double for the vanilla input action consumer: renders children and exposes the
// consumed actions as a data attribute so static markup tests can assert the wiring.
export function InputActionConsumer({ actions, children }: any) {
  return <div data-input-actions={Object.keys(actions ?? {}).join(",")}>{children}</div>;
}

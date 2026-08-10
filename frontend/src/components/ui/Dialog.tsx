import { useEffect, useId, useRef, type PropsWithChildren, type ReactNode, type RefObject } from "react";
import { LoadingSpinner } from "../AsyncState";

interface DialogProps extends PropsWithChildren { open: boolean; title: string; description?: string; footer?: ReactNode; onClose: () => void; initialFocusRef?:RefObject<HTMLElement|null>; }
const focusable='button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export function Dialog({ open, title, description, footer, onClose, initialFocusRef, children }: DialogProps) {
  const card=useRef<HTMLElement>(null); const titleId=useId(); const descriptionId=useId();
  useEffect(() => {
    if (!open) return;
    const previous=document.activeElement as HTMLElement|null; document.body.classList.add("dialog-open");
    const frame=window.requestAnimationFrame(()=>{ (initialFocusRef?.current ?? card.current?.querySelector<HTMLElement>("[autofocus]") ?? card.current?.querySelector<HTMLElement>(focusable) ?? card.current)?.focus(); });
    const keyDown=(event:KeyboardEvent)=>{
      if(event.key==="Escape"){event.preventDefault();onClose();return;}
      if(event.key!=="Tab"||!card.current)return;
      const items=[...card.current.querySelectorAll<HTMLElement>(focusable)].filter(x=>x.offsetParent!==null);
      if(!items.length){event.preventDefault();card.current.focus();return;}
      const first=items[0],last=items[items.length-1];
      if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}
      else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}
    };
    document.addEventListener("keydown",keyDown);
    return()=>{window.cancelAnimationFrame(frame);document.removeEventListener("keydown",keyDown);document.body.classList.remove("dialog-open");previous?.focus();};
  },[open,initialFocusRef]);
  if (!open) return null;
  return <div className="dialog-layer" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}><section ref={card} tabIndex={-1} className="dialog-card" role="dialog" aria-modal="true" aria-labelledby={titleId} aria-describedby={description?descriptionId:undefined}><button type="button" className="dialog-close" onClick={onClose} aria-label="Close dialog">×</button><header><h2 id={titleId}>{title}</h2>{description && <p id={descriptionId}>{description}</p>}</header><div className="dialog-body">{children}</div>{footer && <footer>{footer}</footer>}</section></div>;
}

export function ConfirmDialog({ open, title, description, confirmLabel = "Confirm", cancelLabel="Cancel", destructive, pending, onClose, onConfirm }: { open: boolean; title: string; description: string; confirmLabel?: string; cancelLabel?:string; destructive?: boolean; pending?: boolean; onClose: () => void; onConfirm: () => void }) {
  return <Dialog open={open} title={title} description={description} onClose={pending?()=>{}:onClose} footer={<><button type="button" className="ui-button ghost" disabled={pending} onClick={onClose}>{cancelLabel}</button><button type="button" className={`ui-button ${destructive ? "danger" : "primary"}`} disabled={pending} aria-busy={pending} onClick={onConfirm}>{pending?<><LoadingSpinner size="sm" label="Working" /> Working…</>:confirmLabel}</button></>}><div className="confirmation-note">This action is validated by the server and may be recorded in the audit log.</div></Dialog>;
}

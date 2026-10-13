interface FeedbackBannerProps {
  error: string
  notice: string
  onRetry: () => void
}

export function FeedbackBanner({ error, notice, onRetry }: FeedbackBannerProps) {
  return <>
    {error && <div className="alert" role="alert">{error} <button className="quiet" onClick={onRetry}>Retry connection</button></div>}
    {notice && <p className="notice" role="status">{notice}</p>}
  </>
}

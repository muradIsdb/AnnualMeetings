import { useNavigate } from 'react-router-dom'
import { AlertTriangle, Accessibility, User, Car } from 'lucide-react'
import type { GuestSummary } from '../../types'

interface GuestCardProps {
  guest: GuestSummary
  linkTo?: string
  onSelect?: (guest: GuestSummary) => void
  showStatus?: boolean
}

export default function GuestCard({ guest, linkTo, onSelect, showStatus = true }: GuestCardProps) {
  const navigate = useNavigate()

  const handleClick = () => {
    if (onSelect) onSelect(guest)
    else if (linkTo) navigate(linkTo)
  }

  return (
    <div
      onClick={handleClick}
      className={`card flex items-center gap-4 cursor-pointer hover:shadow-md hover:border-isdb-green/30 transition-all ${
        guest.isCritical ? 'border-l-4 border-l-red-500' : ''
      }`}
    >
      {/* Avatar */}
      <div className="relative flex-shrink-0">
        {guest.photoUrl ? (
          <img
            src={guest.photoUrl}
            alt={guest.fullName}
            className="w-12 h-12 rounded-full object-cover"
          />
        ) : (
          <div className="w-12 h-12 rounded-full bg-isdb-green/10 flex items-center justify-center">
            <User className="w-6 h-6 text-isdb-green" />
          </div>
        )}
        {guest.isCritical && (
          <div className="absolute -top-1 -right-1 w-4 h-4 bg-red-500 rounded-full flex items-center justify-center">
            <AlertTriangle className="w-2.5 h-2.5 text-white" />
          </div>
        )}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <p className="font-semibold text-gray-900 truncate">{guest.fullName}</p>
          {guest.requiresAccessibility && (
            <Accessibility className="w-3.5 h-3.5 text-blue-500 flex-shrink-0" />
          )}
        </div>
        <p className="text-sm text-gray-500 truncate">
          {guest.designation || guest.nationality || '—'}
        </p>
        {guest.notes && (
          <p className="text-xs text-amber-600 truncate mt-0.5">{guest.notes}</p>
        )}
      </div>

      {/* Right side */}
      <div className="flex flex-col items-end gap-1 flex-shrink-0">
        {showStatus && (
          <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full">
            {guest.statusLabel.replace(/([A-Z])/g, ' $1').trim()}
          </span>
        )}
        {guest.activeVehiclePlate && (
          <div className="flex items-center gap-1 text-xs text-isdb-green">
            <Car className="w-3 h-3" />
            <span>{guest.activeVehiclePlate}</span>
          </div>
        )}
        {guest.isCritical && (
          <span className="badge-critical">VIP</span>
        )}
      </div>
    </div>
  )
}

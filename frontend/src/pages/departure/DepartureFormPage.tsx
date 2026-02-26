import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Car, CheckCircle, User, Phone, Mail, MapPin, Plane, Calendar, Clock } from 'lucide-react'
import { departureApi } from '../../api/services'
import { getHotels, getPickupDays, getPickupHours } from '../../api/settingsService'

interface FormState {
  guestName: string
  guestEmail: string
  guestPhone: string
  hotelName: string
  roomNumber: string
  pickupDay: string
  pickupHour: string
  destinationAirport: string
  flightNumber: string
  specialRequirements: string
}

const emptyForm: FormState = {
  guestName: '',
  guestEmail: '',
  guestPhone: '',
  hotelName: '',
  roomNumber: '',
  pickupDay: '',
  pickupHour: '',
  destinationAirport: '',
  flightNumber: '',
  specialRequirements: '',
}

export default function DepartureFormPage() {
  const [submitted, setSubmitted] = useState(false)
  const [form, setForm] = useState<FormState>(emptyForm)

  // Managed dropdown options from the platform settings
  const { data: hotels = [], isLoading: hotelsLoading } = useQuery({
    queryKey: ['hotels-public'],
    queryFn: getHotels,
  })
  const { data: pickupDays = [], isLoading: daysLoading } = useQuery({
    queryKey: ['pickup-days-public'],
    queryFn: getPickupDays,
  })
  const { data: pickupHours = [], isLoading: hoursLoading } = useQuery({
    queryKey: ['pickup-hours-public'],
    queryFn: getPickupHours,
  })

  const submitMutation = useMutation({
    mutationFn: () => {
      const requestedPickupTime = [form.pickupDay, form.pickupHour].filter(Boolean).join(' ')
      return departureApi.create({
        guestName: form.guestName,
        guestEmail: form.guestEmail,
        guestPhone: form.guestPhone,
        hotelName: form.hotelName,
        roomNumber: form.roomNumber,
        requestedPickupTime,
        destinationAirport: form.destinationAirport,
        flightNumber: form.flightNumber,
        specialRequirements: form.specialRequirements,
      })
    },
    onSuccess: () => setSubmitted(true),
    onError: () => toast.error('Failed to submit request. Please try again.'),
  })

  const set = (field: keyof FormState) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>
  ) => setForm(prev => ({ ...prev, [field]: e.target.value }))

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!form.guestName || !form.hotelName || !form.pickupDay || !form.pickupHour || !form.destinationAirport) {
      toast.error('Please fill in all required fields.')
      return
    }
    submitMutation.mutate()
  }

  if (submitted) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-isdb-green to-isdb-green-light flex items-center justify-center p-4">
        <div className="bg-white rounded-2xl shadow-xl p-8 max-w-sm w-full text-center">
          <div className="w-16 h-16 rounded-full bg-green-100 flex items-center justify-center mx-auto mb-4">
            <CheckCircle className="w-8 h-8 text-green-600" />
          </div>
          <h2 className="text-xl font-bold text-gray-900 mb-2">Request Submitted</h2>
          <p className="text-gray-600 text-sm">
            Your departure transport request has been received. Our hospitality team will contact you shortly to confirm the arrangements.
          </p>
          <button
            onClick={() => { setForm(emptyForm); setSubmitted(false) }}
            className="mt-6 bg-green-700 text-white px-6 py-2.5 rounded-lg text-sm font-medium hover:bg-green-800"
          >
            Submit Another Request
          </button>
          <p className="text-xs text-gray-400 mt-4">IsDB Annual Meetings 2026</p>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-isdb-green to-isdb-green-light flex items-center justify-center p-4">
      <div className="w-full max-w-lg">
        {/* Header */}
        <div className="text-center mb-6">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-2xl bg-white shadow-lg mb-3">
            <span className="text-isdb-green font-bold text-lg">IsDB</span>
          </div>
          <h1 className="text-2xl font-bold text-white">Departure Transport Request</h1>
          <p className="text-green-100 text-sm mt-1">Annual Meetings 2026 · Hospitality Services</p>
        </div>

        {/* Form */}
        <div className="bg-white rounded-2xl shadow-xl p-6 space-y-4">

          {/* ── Guest Information ── */}
          <h2 className="font-semibold text-gray-900">Your Information</h2>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Full Name <span className="text-red-500">*</span>
            </label>
            <div className="relative">
              <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
              <input
                type="text"
                value={form.guestName}
                onChange={set('guestName')}
                placeholder="Your full name"
                className="w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Phone</label>
              <div className="relative">
                <Phone className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                <input
                  type="tel"
                  value={form.guestPhone}
                  onChange={set('guestPhone')}
                  placeholder="+966..."
                  className="w-full pl-10 pr-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
                />
              </div>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
              <div className="relative">
                <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                <input
                  type="email"
                  value={form.guestEmail}
                  onChange={set('guestEmail')}
                  placeholder="email@example.com"
                  className="w-full pl-10 pr-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
                />
              </div>
            </div>
          </div>

          {/* ── Hotel (managed dropdown) ── */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Hotel Name <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <MapPin className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
                <select
                  value={form.hotelName}
                  onChange={set('hotelName')}
                  disabled={hotelsLoading}
                  className="w-full pl-10 pr-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green bg-white appearance-none"
                >
                  <option value="">— Select Hotel —</option>
                  {hotels.map(h => (
                    <option key={h.id} value={h.name}>{h.name}</option>
                  ))}
                </select>
              </div>
              {hotels.length === 0 && !hotelsLoading && (
                <p className="text-amber-500 text-xs mt-1">No hotels configured yet.</p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Room Number</label>
              <input
                type="text"
                value={form.roomNumber}
                onChange={set('roomNumber')}
                placeholder="e.g. 412"
                className="w-full px-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
              />
            </div>
          </div>

          <hr className="border-gray-100" />

          {/* ── Departure Details ── */}
          <h2 className="font-semibold text-gray-900">Departure Details</h2>

          {/* Requested Pickup Time — split into Day + Hour */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Requested Pickup Time <span className="text-red-500">*</span>
            </label>
            <div className="grid grid-cols-2 gap-3">
              {/* Day dropdown */}
              <div className="relative">
                <Calendar className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
                <select
                  value={form.pickupDay}
                  onChange={set('pickupDay')}
                  disabled={daysLoading}
                  className="w-full pl-10 pr-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green bg-white appearance-none"
                >
                  <option value="">— Day —</option>
                  {pickupDays.map(d => (
                    <option key={d.id} value={d.value}>{d.label}</option>
                  ))}
                </select>
                {pickupDays.length === 0 && !daysLoading && (
                  <p className="text-amber-500 text-xs mt-1">No days configured.</p>
                )}
              </div>
              {/* Hour dropdown */}
              <div className="relative">
                <Clock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
                <select
                  value={form.pickupHour}
                  onChange={set('pickupHour')}
                  disabled={hoursLoading}
                  className="w-full pl-10 pr-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green bg-white appearance-none"
                >
                  <option value="">— Hour —</option>
                  {pickupHours.map(h => (
                    <option key={h.id} value={h.value}>{h.label}</option>
                  ))}
                </select>
                {pickupHours.length === 0 && !hoursLoading && (
                  <p className="text-amber-500 text-xs mt-1">No hours configured.</p>
                )}
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Destination Airport <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <Plane className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                <input
                  type="text"
                  value={form.destinationAirport}
                  onChange={set('destinationAirport')}
                  placeholder="e.g. JED, RUH"
                  className="w-full pl-10 pr-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
                />
              </div>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Flight Number</label>
              <input
                type="text"
                value={form.flightNumber}
                onChange={set('flightNumber')}
                placeholder="e.g. SV123"
                className="w-full px-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Special Requirements</label>
            <textarea
              value={form.specialRequirements}
              onChange={set('specialRequirements')}
              placeholder="Wheelchair, extra luggage, etc."
              rows={2}
              className="w-full px-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green resize-none"
            />
          </div>

          <button
            onClick={handleSubmit}
            disabled={submitMutation.isPending}
            className="w-full btn-primary py-3 mt-2 flex items-center justify-center gap-2"
          >
            <Car className="w-4 h-4" />
            {submitMutation.isPending ? 'Submitting...' : 'Submit Departure Request'}
          </button>
        </div>
      </div>
    </div>
  )
}

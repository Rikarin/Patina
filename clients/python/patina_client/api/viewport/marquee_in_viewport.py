from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.viewport_marquee import ViewportMarquee
from ...models.viewport_marquee_request import ViewportMarqueeRequest
from typing import cast



def _get_kwargs(
    *,
    body: ViewportMarqueeRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/viewport/marquee",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | ViewportMarquee | None:
    if response.status_code == 200:
        response_200 = ViewportMarquee.from_dict(response.json())



        return response_200

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if response.status_code == 503:
        response_503 = ProblemDetails.from_dict(response.json())



        return response_503

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | ViewportMarquee]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportMarqueeRequest,

) -> Response[ProblemDetails | ViewportMarquee]:
    """ The named meshes that show inside a rectangle in the viewport, which a layer kept to some meshes
    takes or leaves (#110).

    Args:
        body (ViewportMarqueeRequest): Asks which named meshes show inside a rectangle on the
            viewport's picture (#110): a marquee, which takes every
            mesh it reaches into a layer's meshes, or out of them. Through what the viewport draws, as
            a pick is, so a mesh
            hidden behind another or isolated away is not reached.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ViewportMarquee]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportMarqueeRequest,

) -> ProblemDetails | ViewportMarquee | None:
    """ The named meshes that show inside a rectangle in the viewport, which a layer kept to some meshes
    takes or leaves (#110).

    Args:
        body (ViewportMarqueeRequest): Asks which named meshes show inside a rectangle on the
            viewport's picture (#110): a marquee, which takes every
            mesh it reaches into a layer's meshes, or out of them. Through what the viewport draws, as
            a pick is, so a mesh
            hidden behind another or isolated away is not reached.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ViewportMarquee
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportMarqueeRequest,

) -> Response[ProblemDetails | ViewportMarquee]:
    """ The named meshes that show inside a rectangle in the viewport, which a layer kept to some meshes
    takes or leaves (#110).

    Args:
        body (ViewportMarqueeRequest): Asks which named meshes show inside a rectangle on the
            viewport's picture (#110): a marquee, which takes every
            mesh it reaches into a layer's meshes, or out of them. Through what the viewport draws, as
            a pick is, so a mesh
            hidden behind another or isolated away is not reached.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ViewportMarquee]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportMarqueeRequest,

) -> ProblemDetails | ViewportMarquee | None:
    """ The named meshes that show inside a rectangle in the viewport, which a layer kept to some meshes
    takes or leaves (#110).

    Args:
        body (ViewportMarqueeRequest): Asks which named meshes show inside a rectangle on the
            viewport's picture (#110): a marquee, which takes every
            mesh it reaches into a layer's meshes, or out of them. Through what the viewport draws, as
            a pick is, so a mesh
            hidden behind another or isolated away is not reached.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ViewportMarquee
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
